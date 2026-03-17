# Подготовка пустой production-БД для `final_test`

Скрипт `prepare_final_test_prod_seed.ps1` создаёт отдельную БД-клон `final_test_prod_seed`, очищает все прикладные таблицы через `TRUNCATE ... RESTART IDENTITY CASCADE`, выполняет smoke-проверку первых insert, повторно сбрасывает identity и делает логический backup через `pg_dump`.

## Что делает скрипт

1. Читает `Database.ConnectionString` из `Final_Test_Hybrid/appsettings.json`.
2. Ищет `psql.exe` и `pg_dump.exe` в `PATH` и стандартных каталогах PostgreSQL.
3. Создаёт `final_test_prod_seed` как клон текущей `final_test` через `CREATE DATABASE ... TEMPLATE ...`.
4. Очищает прикладные таблицы, не трогая `__EFMigrationsHistory`.
5. Проверяет, что прикладные таблицы пусты, а `__EFMigrationsHistory` не пуста.
6. Выполняет smoke-проверку первых insert для `TB_BOILER_TYPE`, `TB_BOILER_TYPE_CYCLE`, `TB_BOILER`, `TB_OPERATION`.
7. Повторно делает `TRUNCATE ... RESTART IDENTITY CASCADE`, чтобы вернуть sequence к стартовому состоянию.
8. Создаёт SQL backup пустой production-копии.

## Запуск

```powershell
powershell -ExecutionPolicy Bypass -File .\Final_Test_Hybrid\tools\db-maintenance\prepare_final_test_prod_seed.ps1 -DropExisting
```

Backup по умолчанию складывается в `Final_Test_Hybrid/tools/db-maintenance/out`.

## Полезные параметры

- `-TargetDatabase final_test_prod_seed` — имя target БД.
- `-DropExisting` — пересоздать target БД, если она уже существует.
- `-SkipBackup` — пропустить шаг `pg_dump`.
- `-ForceSourceDisconnect` — завершить активные подключения к source БД перед клонированием.
- `-AppSettingsPath <path>` — взять connection string из другого `appsettings.json`.
- `-BackupDirectory <path>` — каталог для итогового `.sql` backup.

## Ограничения

- Для `CREATE DATABASE ... TEMPLATE ...` source БД должна быть без активных подключений. Иначе скрипт завершится ошибкой или потребует `-ForceSourceDisconnect`.
- Скрипт не трогает рабочую `final_test`, кроме чтения и использования её как шаблона.
- После restore production-копии стенд нельзя запускать в работу, пока заново не будут заполнены `BoilerType`, `BoilerTypeCycle`, `Recipe`, `ResultSettings`, `StepFinalTest`, `ErrorSettingsTemplate`.

