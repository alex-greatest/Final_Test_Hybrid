[CmdletBinding()]
param(
    [string]$AppSettingsPath,
    [string]$TargetDatabase = "final_test_prod_seed",
    [string]$MaintenanceDatabase = "postgres",
    [string]$BackupDirectory,
    [switch]$DropExisting,
    [switch]$SkipBackup,
    [switch]$ForceSourceDisconnect
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-DefaultPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $RelativePath))
}

function Get-EffectiveSetting {
    param(
        [string]$Value,
        [Parameter(Mandatory = $true)]
        [string]$Fallback
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Fallback
    }

    return [System.IO.Path]::GetFullPath($Value)
}

function Read-DatabaseSettings {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $json = Get-Content -Raw -Encoding UTF8 $Path | ConvertFrom-Json
    $connectionString = [string]$json.Database.ConnectionString
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        throw "В $Path не найден Database.ConnectionString"
    }

    $values = @{}
    $parts = $connectionString.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)
    foreach ($part in $parts) {
        $pair = $part.Split('=', 2)
        if ($pair.Length -ne 2) {
            continue
        }

        $key = $pair[0].Trim().ToLowerInvariant()
        $values[$key] = $pair[1].Trim()
    }

    return [pscustomobject]@{
        Host     = [string]$values["host"]
        Port     = [string]$(if ($values.ContainsKey("port")) { $values["port"] } else { "5432" })
        Database = [string]$values["database"]
        Username = [string]$(if ($values.ContainsKey("username")) { $values["username"] } else { $values["user id"] })
        Password = [string]$values["password"]
    }
}

function Find-ToolPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolName
    )

    $command = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $baseDir = Join-Path $env:ProgramFiles "PostgreSQL"
    if (-not (Test-Path $baseDir)) {
        throw "Не найден каталог PostgreSQL в $baseDir"
    }

    $match = Get-ChildItem $baseDir -Recurse -Filter "$ToolName.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName |
        Select-Object -First 1 -ExpandProperty FullName

    if ([string]::IsNullOrWhiteSpace($match)) {
        throw "Не найден $ToolName.exe"
    }

    return $match
}

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Executable,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [string]$Password
    )

    $previousPassword = $env:PGPASSWORD
    if ($null -ne $Password) {
        $env:PGPASSWORD = $Password
    }

    try {
        $output = & $Executable @Arguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            $message = ($output | Out-String).Trim()
            throw "Команда завершилась с кодом ${LASTEXITCODE}: $Executable`n$message"
        }

        return ($output | Out-String).Trim()
    }
    finally {
        $env:PGPASSWORD = $previousPassword
    }
}

function Get-PsqlArguments {
    param(
        [Parameter(Mandatory = $true)]
        $Settings,
        [Parameter(Mandatory = $true)]
        [string]$Database
    )

    return @(
        "-v", "ON_ERROR_STOP=1",
        "-h", $Settings.Host,
        "-p", $Settings.Port,
        "-U", $Settings.Username,
        "-d", $Database
    )
}

function Invoke-PsqlScalar {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PsqlPath,
        [Parameter(Mandatory = $true)]
        $Settings,
        [Parameter(Mandatory = $true)]
        [string]$Database,
        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $arguments = Get-PsqlArguments -Settings $Settings -Database $Database
    $arguments += @("-At", "-c", $Sql)
    return Invoke-ExternalCommand -Executable $PsqlPath -Arguments $arguments -Password $Settings.Password
}

function Invoke-PsqlCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PsqlPath,
        [Parameter(Mandatory = $true)]
        $Settings,
        [Parameter(Mandatory = $true)]
        [string]$Database,
        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $arguments = Get-PsqlArguments -Settings $Settings -Database $Database
    $arguments += @("-c", $Sql)
    $null = Invoke-ExternalCommand -Executable $PsqlPath -Arguments $arguments -Password $Settings.Password
}

function Invoke-PsqlFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PsqlPath,
        [Parameter(Mandatory = $true)]
        $Settings,
        [Parameter(Mandatory = $true)]
        [string]$Database,
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath
    )

    $arguments = Get-PsqlArguments -Settings $Settings -Database $Database
    $arguments += @("-f", $ScriptPath)
    $result = Invoke-ExternalCommand -Executable $PsqlPath -Arguments $arguments -Password $Settings.Password
    if (-not [string]::IsNullOrWhiteSpace($result)) {
        Write-Host $result
    }
}

function Quote-Identifier {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return '"' + $Value.Replace('"', '""') + '"'
}

function Quote-Literal {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return "'" + $Value.Replace("'", "''") + "'"
}

function Test-DatabaseExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PsqlPath,
        [Parameter(Mandatory = $true)]
        $Settings,
        [Parameter(Mandatory = $true)]
        [string]$DatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$MaintenanceDb
    )

    $sql = "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = $(Quote-Literal -Value $DatabaseName));"
    return (Invoke-PsqlScalar -PsqlPath $PsqlPath -Settings $Settings -Database $MaintenanceDb -Sql $sql) -eq "t"
}

function Get-ActiveConnectionCount {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PsqlPath,
        [Parameter(Mandatory = $true)]
        $Settings,
        [Parameter(Mandatory = $true)]
        [string]$DatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$MaintenanceDb
    )

    $sql = "SELECT COUNT(*) FROM pg_stat_activity WHERE datname = $(Quote-Literal -Value $DatabaseName) AND pid <> pg_backend_pid();"
    return [int](Invoke-PsqlScalar -PsqlPath $PsqlPath -Settings $Settings -Database $MaintenanceDb -Sql $sql)
}

function Disconnect-DatabaseUsers {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PsqlPath,
        [Parameter(Mandatory = $true)]
        $Settings,
        [Parameter(Mandatory = $true)]
        [string]$DatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$MaintenanceDb
    )

    $sql = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = $(Quote-Literal -Value $DatabaseName) AND pid <> pg_backend_pid();"
    Invoke-PsqlCommand -PsqlPath $PsqlPath -Settings $Settings -Database $MaintenanceDb -Sql $sql
}

function Ensure-CloneCanBeCreated {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PsqlPath,
        [Parameter(Mandatory = $true)]
        $Settings,
        [Parameter(Mandatory = $true)]
        [string]$SourceDatabase,
        [Parameter(Mandatory = $true)]
        [string]$MaintenanceDb,
        [switch]$ForceDisconnect
    )

    $activeConnections = Get-ActiveConnectionCount -PsqlPath $PsqlPath -Settings $Settings -DatabaseName $SourceDatabase -MaintenanceDb $MaintenanceDb
    if ($activeConnections -eq 0) {
        return
    }

    if (-not $ForceDisconnect) {
        throw "В source БД $SourceDatabase есть активные подключения: $activeConnections. Закройте приложения или повторите с -ForceSourceDisconnect."
    }

    Disconnect-DatabaseUsers -PsqlPath $PsqlPath -Settings $Settings -DatabaseName $SourceDatabase -MaintenanceDb $MaintenanceDb
}

function Prepare-TargetDatabase {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PsqlPath,
        [Parameter(Mandatory = $true)]
        $Settings,
        [Parameter(Mandatory = $true)]
        [string]$SourceDatabase,
        [Parameter(Mandatory = $true)]
        [string]$TargetDatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$MaintenanceDb,
        [switch]$AllowDropExisting,
        [switch]$ForceDisconnectSource
    )

    Ensure-CloneCanBeCreated -PsqlPath $PsqlPath -Settings $Settings -SourceDatabase $SourceDatabase -MaintenanceDb $MaintenanceDb -ForceDisconnect:$ForceDisconnectSource
    $targetExists = Test-DatabaseExists -PsqlPath $PsqlPath -Settings $Settings -DatabaseName $TargetDatabaseName -MaintenanceDb $MaintenanceDb
    if ($targetExists -and -not $AllowDropExisting) {
        throw "БД $TargetDatabaseName уже существует. Повторите с -DropExisting."
    }

    if ($targetExists) {
        Disconnect-DatabaseUsers -PsqlPath $PsqlPath -Settings $Settings -DatabaseName $TargetDatabaseName -MaintenanceDb $MaintenanceDb
        $dropSql = "DROP DATABASE $(Quote-Identifier -Value $TargetDatabaseName);"
        Invoke-PsqlCommand -PsqlPath $PsqlPath -Settings $Settings -Database $MaintenanceDb -Sql $dropSql
    }

    $createSql = "CREATE DATABASE $(Quote-Identifier -Value $TargetDatabaseName) TEMPLATE $(Quote-Identifier -Value $SourceDatabase);"
    Invoke-PsqlCommand -PsqlPath $PsqlPath -Settings $Settings -Database $MaintenanceDb -Sql $createSql
}

function New-BackupFilePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Directory,
        [Parameter(Mandatory = $true)]
        [string]$DatabaseName
    )

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    return Join-Path $Directory "$DatabaseName-$timestamp.sql"
}

function Invoke-PgDumpBackup {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PgDumpPath,
        [Parameter(Mandatory = $true)]
        $Settings,
        [Parameter(Mandatory = $true)]
        [string]$DatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$BackupPath
    )

    $arguments = @(
        "-h", $Settings.Host,
        "-p", $Settings.Port,
        "-U", $Settings.Username,
        "-d", $DatabaseName,
        "--clean",
        "--if-exists",
        "--create",
        "--no-owner",
        "--no-privileges",
        "--format=plain",
        "--file", $BackupPath
    )

    $null = Invoke-ExternalCommand -Executable $PgDumpPath -Arguments $arguments -Password $Settings.Password
}

$effectiveAppSettingsPath = Get-EffectiveSetting -Value $AppSettingsPath -Fallback (Get-DefaultPath -RelativePath "..\..\appsettings.json")
$effectiveBackupDirectory = Get-EffectiveSetting -Value $BackupDirectory -Fallback (Get-DefaultPath -RelativePath "out")
$truncateScriptPath = Get-DefaultPath -RelativePath "final_test_prod_seed_truncate.sql"
$verifyScriptPath = Get-DefaultPath -RelativePath "final_test_prod_seed_verify.sql"
$smokeScriptPath = Get-DefaultPath -RelativePath "final_test_prod_seed_smoke.sql"

$settings = Read-DatabaseSettings -Path $effectiveAppSettingsPath
if ([string]::IsNullOrWhiteSpace($settings.Database)) {
    throw "В connection string не задано имя source БД"
}

if ($settings.Database -eq $TargetDatabase) {
    throw "TargetDatabase совпадает с source БД $($settings.Database)"
}

$psqlPath = Find-ToolPath -ToolName "psql"
$pgDumpPath = Find-ToolPath -ToolName "pg_dump"

Write-Host "Source БД: $($settings.Database)"
Write-Host "Target БД: $TargetDatabase"
Write-Host "psql: $psqlPath"
Write-Host "pg_dump: $pgDumpPath"

Prepare-TargetDatabase `
    -PsqlPath $psqlPath `
    -Settings $settings `
    -SourceDatabase $settings.Database `
    -TargetDatabaseName $TargetDatabase `
    -MaintenanceDb $MaintenanceDatabase `
    -AllowDropExisting:$DropExisting `
    -ForceDisconnectSource:$ForceSourceDisconnect

Invoke-PsqlFile -PsqlPath $psqlPath -Settings $settings -Database $TargetDatabase -ScriptPath $truncateScriptPath
Invoke-PsqlFile -PsqlPath $psqlPath -Settings $settings -Database $TargetDatabase -ScriptPath $verifyScriptPath
Invoke-PsqlFile -PsqlPath $psqlPath -Settings $settings -Database $TargetDatabase -ScriptPath $smokeScriptPath
Invoke-PsqlFile -PsqlPath $psqlPath -Settings $settings -Database $TargetDatabase -ScriptPath $truncateScriptPath
Invoke-PsqlFile -PsqlPath $psqlPath -Settings $settings -Database $TargetDatabase -ScriptPath $verifyScriptPath

if (-not $SkipBackup) {
    New-Item -ItemType Directory -Force -Path $effectiveBackupDirectory | Out-Null
    $backupPath = New-BackupFilePath -Directory $effectiveBackupDirectory -DatabaseName $TargetDatabase
    Invoke-PgDumpBackup -PgDumpPath $pgDumpPath -Settings $settings -DatabaseName $TargetDatabase -BackupPath $backupPath
    Write-Host "Логический backup создан: $backupPath"
}

Write-Host "Подготовка $TargetDatabase завершена."


