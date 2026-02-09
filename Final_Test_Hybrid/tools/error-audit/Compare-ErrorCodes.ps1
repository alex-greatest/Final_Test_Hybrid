[CmdletBinding()]
param(
    [string]$DatabaseHost = "localhost",
    [int]$DatabasePort = 5432,
    [string]$DatabaseName = "traceability_boiler",
    [string]$DatabaseUser = "postgres",
    [string]$DatabasePassword = $env:PGPASSWORD,
    [int]$StationTypeId = 7,
    [ValidateSet("AllDeclarations", "Active")]
    [string]$Scope = "AllDeclarations",
    [string]$SolutionRoot = "",
    [string]$ReportsDir = ""
)

$ErrorActionPreference = "Stop"

function Get-RequiredPath {
    param([string]$PathToCheck, [string]$EntityName)

    if (-not (Test-Path -Path $PathToCheck)) {
        throw "Не найден ${EntityName}: $PathToCheck"
    }

    return (Resolve-Path $PathToCheck).Path
}

$scriptRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) { Split-Path -Parent $MyInvocation.MyCommand.Path } else { $PSScriptRoot }
if ([string]::IsNullOrWhiteSpace($SolutionRoot)) {
    $SolutionRoot = (Resolve-Path (Join-Path $scriptRoot "..\..\..")).Path
}

if ([string]::IsNullOrWhiteSpace($ReportsDir)) {
    $ReportsDir = Join-Path $scriptRoot "reports"
}

$errorsDir = Get-RequiredPath -PathToCheck (Join-Path $SolutionRoot "Final_Test_Hybrid\Models\Errors") -EntityName "путь к ErrorDefinitions"

if (-not (Test-Path -Path $ReportsDir)) {
    New-Item -Path $ReportsDir -ItemType Directory | Out-Null
}

$reportsDirResolved = (Resolve-Path $ReportsDir).Path
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $reportsDirResolved "error-code-diff-$timestamp.txt"

$tempDir = Join-Path $env:TEMP ("error-audit-" + [Guid]::NewGuid().ToString("N"))
New-Item -Path $tempDir -ItemType Directory | Out-Null

$projectPath = Join-Path $tempDir "ErrorAuditRunner.csproj"
$programPath = Join-Path $tempDir "Program.cs"

$projectContent = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql" Version="10.0.0" />
  </ItemGroup>
</Project>
'@

$programContent = @'
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

var argsMap = ParseArgs(args);
var errorsDir = Require(argsMap, "--errors-dir");
var host = Require(argsMap, "--host");
var database = Require(argsMap, "--db");
var user = Require(argsMap, "--user");
var password = argsMap.TryGetValue("--password", out var passArg) ? passArg : string.Empty;
var scopeValue = Require(argsMap, "--scope");

if (!int.TryParse(Require(argsMap, "--port"), out var port))
{
    throw new ArgumentException("Некорректный --port");
}

if (!int.TryParse(Require(argsMap, "--station"), out var stationTypeId))
{
    throw new ArgumentException("Некорректный --station");
}

if (!Enum.TryParse<CompareScope>(scopeValue, ignoreCase: true, out var scope))
{
    throw new ArgumentException("Допустимые значения --scope: AllDeclarations, Active");
}

if (!Directory.Exists(errorsDir))
{
    throw new DirectoryNotFoundException($"Не найден каталог ошибок: {errorsDir}");
}

var files = Directory.GetFiles(errorsDir, "ErrorDefinitions*.cs", SearchOption.TopDirectoryOnly);
if (files.Length == 0)
{
    throw new InvalidOperationException($"В каталоге {errorsDir} не найдено файлов ErrorDefinitions*.cs");
}

var codeSet = scope switch
{
    CompareScope.AllDeclarations => GetAllDeclarationCodes(files),
    CompareScope.Active => GetActiveCodes(files),
    _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Неизвестный scope")
};

var dbSet = await GetDatabaseCodesAsync(host, port, database, user, password, stationTypeId);
var missing = codeSet.Except(dbSet, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
var extra = dbSet.Except(codeSet, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

var report = BuildReport(scope, errorsDir, files.Length, stationTypeId, codeSet.Count, dbSet.Count, missing, extra);
Console.OutputEncoding = Encoding.UTF8;
Console.Write(report);

static string BuildReport(
    CompareScope scope,
    string errorsDir,
    int sourceFiles,
    int stationTypeId,
    int codeCount,
    int dbCount,
    IReadOnlyList<string> missing,
    IReadOnlyList<string> extra)
{
    var sb = new StringBuilder();
    sb.AppendLine($"generated_at_utc: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
    sb.AppendLine($"scope: {scope}");
    sb.AppendLine($"errors_dir: {errorsDir}");
    sb.AppendLine($"source_files: {sourceFiles}");
    sb.AppendLine($"station_type_id: {stationTypeId}");
    sb.AppendLine($"code_count: {codeCount}");
    sb.AppendLine($"db_count: {dbCount}");
    sb.AppendLine($"missing_in_db: {missing.Count}");
    foreach (var code in missing)
    {
        sb.AppendLine($"MISSING:{code}");
    }

    sb.AppendLine($"extra_in_db: {extra.Count}");
    foreach (var code in extra)
    {
        sb.AppendLine($"EXTRA:{code}");
    }

    return sb.ToString();
}

static HashSet<string> GetAllDeclarationCodes(IEnumerable<string> files)
{
    var declarationRegex = new Regex(
        @"public\s+static\s+readonly\s+ErrorDefinition\s+\w+\s*=\s*new\(\s*""([^""]+)""",
        RegexOptions.Multiline);

    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var file in files)
    {
        var text = File.ReadAllText(file);
        foreach (Match match in declarationRegex.Matches(text))
        {
            var code = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(code))
            {
                set.Add(code);
            }
        }
    }

    return set;
}

static HashSet<string> GetActiveCodes(IEnumerable<string> files)
{
    var declarationRegex = new Regex(
        @"public\s+static\s+readonly\s+ErrorDefinition\s+(\w+)\s*=\s*new\(\s*""([^""]+)""",
        RegexOptions.Multiline);

    var listRegex = new Regex(
        @"internal\s+static\s+IEnumerable<ErrorDefinition>\s+\w+\s*=>\s*\[(.*?)\];",
        RegexOptions.Singleline);

    var definitions = new Dictionary<string, string>(StringComparer.Ordinal);
    var listedNames = new HashSet<string>(StringComparer.Ordinal);

    foreach (var file in files)
    {
        var text = File.ReadAllText(file);

        foreach (Match match in declarationRegex.Matches(text))
        {
            definitions[match.Groups[1].Value] = match.Groups[2].Value.Trim();
        }

        foreach (Match listMatch in listRegex.Matches(text))
        {
            var body = listMatch.Groups[1].Value;
            var tokens = body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                if (!token.StartsWith("..", StringComparison.Ordinal))
                {
                    listedNames.Add(token.Trim());
                }
            }
        }
    }

    var activeCodes = listedNames
        .Where(definitions.ContainsKey)
        .Select(name => definitions[name])
        .Where(code => !string.IsNullOrWhiteSpace(code))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    return activeCodes;
}

static async Task<HashSet<string>> GetDatabaseCodesAsync(
    string host,
    int port,
    string database,
    string user,
    string password,
    int stationTypeId)
{
    var connectionString = $"Host={host};Port={port};Database={database};Username={user};Password={password}";
    var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();

    const string sql = """
        select distinct address_error
        from tb_error_settings_template
        where station_type_id = @stationTypeId
          and address_error is not null
          and btrim(address_error) <> ''
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("stationTypeId", stationTypeId);

    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var value = reader.GetString(0).Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            result.Add(value);
        }
    }

    return result;
}

static Dictionary<string, string> ParseArgs(IReadOnlyList<string> args)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Count; i++)
    {
        var key = args[i];
        if (!key.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var value = i + 1 < args.Count ? args[i + 1] : string.Empty;
        map[key] = value;
        i++;
    }

    return map;
}

static string Require(IReadOnlyDictionary<string, string> map, string key)
{
    if (!map.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
    {
        throw new ArgumentException($"Не задан обязательный аргумент {key}");
    }

    return value;
}

enum CompareScope
{
    AllDeclarations,
    Active
}
'@

$projectContent | Set-Content -Path $projectPath -Encoding UTF8
$programContent | Set-Content -Path $programPath -Encoding UTF8

$passwordValue = if ($null -eq $DatabasePassword) { "" } else { $DatabasePassword }

$runArgs = @(
    "run",
    "--project", $projectPath,
    "--configuration", "Release",
    "-v", "q",
    "--",
    "--errors-dir", $errorsDir,
    "--host", $DatabaseHost,
    "--port", $DatabasePort.ToString(),
    "--db", $DatabaseName,
    "--user", $DatabaseUser,
    "--password", $passwordValue,
    "--station", $StationTypeId.ToString(),
    "--scope", $Scope
)

try {
    $output = & dotnet @runArgs 2>&1
    $exitCode = $LASTEXITCODE
    $outputText = [string]::Join([Environment]::NewLine, $output)
    $outputText | Set-Content -Path $reportPath -Encoding UTF8

    if ($exitCode -ne 0) {
        Write-Host $outputText
        throw "Сравнение завершилось с ошибкой. Отчёт: $reportPath"
    }

    Write-Host $outputText
    Write-Host "ReportPath: $reportPath"
}
finally {
    if (Test-Path -Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force
    }
}
