# Final_Test_Hybrid

## Критичность

> **SCADA-система промышленных тестов. От кода зависят жизни — думай дважды, проверяй трижды.**

## Рабочий стиль (обязательно)

- Сначала факты, потом выводы: гипотезы проверяются кодом и документами, а не “по аналогии”.
- Сначала проектные документы, потом изменение логики (`Docs/*Guide.md`, `plan-refactor-executor.md`, связанные инциденты).
- Если документ противоречит гипотезе — корректировать гипотезу, а не продавливать мнение.
- Выявляй слабую логику и риски прямо, без смягчений и расплывчатости.
- Запрет: не использовать `ColumnExecutor.IsVisible` как критерий idle/готовности к следующему Map.

## Что избегать

- Скрытых побочных эффектов в runtime-потоках (автоматика не должна вмешиваться в ручные сценарии без явного контекста).
- “UI-фикс без pipeline-guard”: визуальная блокировка без runtime-проверки недостаточна.
- Небезопасных preset-операций в диагностике (только безопасный минимум; остальное — ручной режим).
- Глобальных поведенческих изменений “по умолчанию” там, где нужен только точечный opt-in.
- Бесконечных ожиданий/retry без дедлайнов и fail-safe выхода.
- Дублирующих путей сохранения параметров и множественных источников списка (ломают согласованность).

## UI-инварианты (актуально)

- Единый стиль таблиц применять только через opt-in классы `grid-unified-host` + `grid-unified` из `wwwroot/css/app.css`.
- В родительских вкладках запрещены широкие `::deep .rz-data-grid*` override: shell/layout можно, типографику и геометрию таблицы — нельзя.
- Исключение зафиксировано: `Components/Main/TestSequenseGrid.razor` использует `main-grid-legacy` (компактный исторический вид главного экрана).
- Для обрезания заголовков DataGrid править не только `th`, но и внутренние контейнеры (`.rz-cell-data`, `.rz-column-title-content`, `.rz-sortable-column`, `.rz-column-title`).
- Новые вкладочные контейнеры обязаны занимать всю высоту родителя (`display:flex`, `flex-direction:column`, `min-height:0`, `height:100%`), ориентир — `RecipesGrid`.
- `LogViewerTab`: внутренняя структура зафиксирована как две вкладки — `Лог-файл` и `Время шагов`; `StepTimingsGrid` живёт во вкладке `Лог`, не в `Results`.

## Stack

| Компонент | Технология |
|-----------|------------|
| Framework | .NET 10, WinForms + Blazor Hybrid |
| UI | Radzen Blazor 8.3 |
| OPC-UA | OPCFoundation 1.5 |
| Modbus | NModbus 3.0 |
| Database | PostgreSQL + EF Core 10 |
| Logging | Serilog + DualLogger |
| Excel | EPPlus 8.3 |

**Build:** `dotnet build && dotnet run`
**Обязательная проверка:** минимум `dotnet build`.

## Архитектурный контур

`Program.cs -> Form1.cs (DI) -> BlazorWebView -> Radzen UI`

`[Barcode] -> PreExecutionCoordinator -> TestExecutionCoordinator -> [OK/NOK]`

`ScanSteps (pre-exec)` + `4 x ColumnExecutor`.

## Ключевые паттерны

### DualLogger

```csharp
public class MyService(DualLogger<MyService> logger)
{
    logger.LogInformation("msg"); // файл + UI теста
}
```

### Pausable decorators

- В тестовых шагах использовать pausable-сервисы (`PausableOpcUaTagService`, `PausableTagWaiter`, `PausableRegisterReader/Writer`).
- Системные операции — через не-pausable сервисы (`OpcUaTagService`, `RegisterReader/Writer`).
- В шагах использовать `context.DelayAsync()`, `context.DiagReader/Writer`.

### Primary constructors

```csharp
public class MyStep(DualLogger<MyStep> logger, IOpcUaTagService tags) : ITestStep
```

## Правила кодирования

- Простота и читаемость важнее “умности”.
- Без overengineering и лишнего defense programming внутри внутреннего кода.
- Один основной управляющий блок на метод (`if/for/while/switch/try`), guard clauses допустимы.
- Метод около 50 строк; разрастание — сразу вынос в private helper-методы того же partial.
- `var` по умолчанию, `{}` обязательны.
- Сервисы до ~300 строк, дальше дробить на partial.
- Именование: `PascalCase` для типов/методов, `camelCase` для локальных/параметров.

## Контракт параметров результатов (обязательно)

- Имя параметра — каноническое и единое во всех местах (без вариантов написания).
- `isRanged = true` только если параметр реально имеет `min/max` по контракту потребителя.
- `isRanged = false` для строковых/информационных параметров без пределов.
- Метаданные (`isRanged`, единицы, границы) задавать по контракту каждого параметра отдельно; не копировать “по соседству”.
- Для групповых операций использовать единый источник списка (single source of truth), дубли вне списка удалять.
- Для retry перед повторной записью удалять старый результат (`testResultsService.Remove(...)`).

## Практики устойчивого кода

- Критический cleanup (`Release/Dispose/сброс флагов`) — в `finally`.
- Публичные события в критических путях вызывать через safe-обёртки с логированием исключений.
- Reconnect: bounded retry (2-3 попытки, 200-500 ms), только для transient OPC ошибок.
- После reconnect перед `Write/Wait` по PLC-тегам ждать `connectionState.WaitForConnectionAsync(ct)`.
- Reset-flow не должен ждать бесконечно: ограничивать таймаутами из `OpcUa:ResetFlowTimeouts`.
- Ошибки записи критичных PLC-тегов (`Reset`, `AskRepeat`, `End`) не проглатывать.
- При rebind/reset подписок инвалидировать runtime-кэш и не сохранять Bad-quality значения.
- Ошибки `AddTagAsync/ApplyChangesAsync` — с rollback runtime-состояния.
- В `TagWaiter.WaitForFalseAsync` первичная cache-проверка через `subscription.GetValue(nodeId) + is bool`.
- Не-`OperationCanceledException` в фоновом retry — fail-fast в `HardReset` + fail-safe `OpenGate()`.
- Ошибка `SaveAsync` в completion-flow не должна ронять main loop: переводить в recoverable save-failure.
- Для `PlcResetCoordinator.PlcHardResetPending` сброс в `0` обязателен в `finally` вокруг `_errorCoordinator.Reset()`.

## Зафиксированные инварианты (не пересматривать без нового инцидента)

- `PLC reset` (через `Req_Reset`/`PlcResetCoordinator`) и `HardReset` (через `ErrorCoordinator.OnReset`) — разные потоки.
- В гонке `testCompletion vs reset` приоритет у reset.
- Soft-reset не должен ломать текущий `AskEnd`-путь очистки UI/Boiler.
- Логику changeover-таймера не менять правками reset/reconnect.
- Для runtime OPC-подписок при reconnect использовать только полный rebuild (`новая Session + RecreateForSessionAsync`).
- Спиннер подписки показывать только при фактическом запуске подписок после готовности соединения.
- `Coms/Check_Comms` при `AutoReady = false`: шаг завершается `NoDiagnosticConnection`, `IModbusDispatcher` останавливается.
- Fail результата шага фиксировать в `ColumnExecutor` до `pauseToken.WaitWhilePausedAsync`.
- Для non-PLC шагов запись `BaseTags.Fault` обязательна с bounded retry; при провале — fail-fast `HardReset`.
- Для execution runtime-подписок использовать `IRequiresPlcSubscriptions`; `IRequiresPlcTags` оставить базовым/валидационным контрактом.
- `BaseTags.AskEnd` считать системным preload-тегом.
- В pre-execution при потере OPC блокировать оба канала barcode (`BoilerInfo`, `BarcodeDebounceHandler`) и ставить scan-таймер на паузу.
- Для ECU error flow по ping: ошибка поднимается только в lock-контексте (`1005 in {1,2}` + whitelist), вне lock очищается.

## Язык и кодировка

- Новые/переименованные комментарии и документация — на русском языке.
- Файлы в `UTF-8`; если файл уже с BOM, сохранять с BOM.
- При работе через CLI явно читать текст как UTF-8 (`Get-Content -Encoding UTF8`).
- Если появляются строки вида `РџР.../РѕР...` — исправлять в том же изменении и перечитывать файл в UTF-8.

## Документация

| Тема | Файл |
|------|------|
| State Management | `Final_Test_Hybrid/Docs/StateManagementGuide.md` |
| Error Handling | `Final_Test_Hybrid/Docs/ErrorCoordinatorGuide.md` |
| PLC Reset | `Final_Test_Hybrid/Docs/PlcResetGuide.md` |
| Steps | `Final_Test_Hybrid/Docs/StepsGuide.md` |
| Cancellation | `Final_Test_Hybrid/Docs/CancellationGuide.md` |
| Modbus | `Final_Test_Hybrid/Docs/DiagnosticGuide.md` |
| TagWaiter | `Final_Test_Hybrid/Docs/TagWaiterGuide.md` |
| Scanner | `Final_Test_Hybrid/Docs/ScannerGuide.md` |

Детальная документация: `Final_Test_Hybrid/CLAUDE.md`.

## LEARNING LOG (обязательно)

- После каждого значимого изменения фиксировать: `Что изменили`, `Почему`, `Риск/урок`, `Ссылки`.
- `Final_Test_Hybrid/LEARNING_LOG.md` держать коротким (активный индекс): последние 30 дней или максимум 40 записей.
- Старые записи переносить в `Final_Test_Hybrid/LEARNING_LOG_ARCHIVE.md` без потери фактов.

Шаблон записи:

```md
### YYYY-MM-DD (тема)
- Что изменили:
- Почему:
- Риск/урок:
- Ссылки: `path1`, `path2`
```

## Периодичность и quality-gates

- После каждого значимого изменения в логике (reset/reconnect/error-flow) — минимум `dotnet build`.
- `jb inspectcode` запускать по изменённым `*.cs` после завершения логического блока (не после каждой косметики).
- Перед сдачей изменений обязательный чек-лист:
  1. `dotnet build Final_Test_Hybrid.slnx`
  2. `dotnet format analyzers --verify-no-changes`
  3. `dotnet format style --verify-no-changes`

Пример точечного `inspectcode`:

```powershell
$changedCs = git diff --name-only --diff-filter=ACMR HEAD -- '*.cs'
if ($changedCs.Count -gt 0)
{
    $include = ($changedCs | ForEach-Object { $_.Replace('\\', '/') }) -join ';'
    $reportPath = Join-Path $env:TEMP 'jb-inspectcode.txt'
    jb inspectcode Final_Test_Hybrid.slnx "--include=$include" --no-build --format=Text "--output=$reportPath" -e=WARNING
    Write-Host "InspectCode report: $reportPath"
}
```

## Временные компромиссы

- Исторические кодировочные артефакты могут существовать локально; нормализация выполняется отдельной задачей.
- Для runtime OPC reconnect текущий компромисс: только полный rebuild, без гибридного ручного rebind.
- Отдельная ветка UI-текстов по `PlcConnectionLost` пока не вводится; используется текущая логика сообщений.
