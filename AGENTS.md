# Final_Test_Hybrid

## Критичность

> **SCADA-система промышленных тестов. От кода зависят жизни — думай дважды, проверяй трижды.**

## Рабочий стиль (обязательно)

- ***Важно*** Перед и после правок проверять на соотвествиие "Правила кодирования". Чистоту кода.
- Сначала факты, потом выводы: гипотезы проверяются кодом и документами, а не “по аналогии”.
- Сначала проектные документы, потом изменение логики (`Docs/*Guide.md`, `plan-refactor-executor.md`, связанные инциденты).
- Если документ противоречит гипотезе — корректировать гипотезу, а не продавливать мнение.
- Выявлять слабую логику и риски прямо, без смягчений и расплывчатости.
- Запрет: не использовать `ColumnExecutor.IsVisible` как критерий idle/готовности к следующему Map.

## Что избегать

- Скрытых побочных эффектов в runtime-потоках (автоматика не должна вмешиваться в ручные сценарии без явного контекста).
- “UI-фикс без pipeline-guard”: визуальная блокировка без runtime-проверки недостаточна.
- Небезопасных preset-операций в диагностике (только безопасный минимум; остальное — ручной режим).
- Глобальных поведенческих изменений “по умолчанию” там, где нужен только точечный opt-in.
- Бесконечных ожиданий/retry без дедлайнов и fail-safe выхода.
- Дублирующих путей сохранения параметров и множественных источников списка (ломают согласованность).

## Runtime-критичные правила (Do/Don't)

Только operational-правила для runtime-критичных веток (без исторических деталей и UI-косметики).

### Do

- Явно разделять `PLC reset` и `HardReset`; в гонках исполнения приоритет всегда у reset-потока.
- Перед любыми `Write/Wait` после reconnect ждать `connectionState.WaitForConnectionAsync(ct)`.
- Для execution retry-flow ожидание connection-ready через `WaitForConnectionAsync(ct)` выполняется без локального timeout: система рассчитана на внешнее прерывание от PLC (`PlcConnectionLost -> Reset -> Stop/Cancel`). В этом контексте отдельный timeout не требуется.
- Для runtime OPC-подписок использовать только полный rebuild (`new Session + RecreateForSessionAsync`).
- Использовать bounded retry только для transient-ошибок (обычно 2-3 попытки, 200-500 мс).
- Ограничивать reset-flow таймаутами из `OpcUa:ResetFlowTimeouts`; без бесконечных ожиданий.
- Фиксировать fail результата шага в `ColumnExecutor` до `pauseToken.WaitWhilePausedAsync`.
- Для `Coms/Check_Comms` при `AutoReady = false` завершать шаг `NoDiagnosticConnection` и останавливать `IModbusDispatcher`.
- Для non-PLC шагов писать `BaseTags.Fault` с bounded retry; при провале — fail-fast `HardReset`.
- Перед retry-записью результатов удалять старую запись (`testResultsService.Remove(...)`) и писать заново.
- Ошибки `SaveAsync` в completion-flow переводить в recoverable save-failure, не роняя main loop.
- Держать критический cleanup в `finally` (`Release/Dispose/сброс флагов`, включая `PlcHardResetPending`).
- Публичные события в критических путях вызывать через safe-обёртки с логированием исключений.
- Ошибки `AddTagAsync/ApplyChangesAsync` обрабатывать с rollback runtime-состояния.
- В `TagWaiter.WaitForFalseAsync` делать первичную cache-проверку через `subscription.GetValue(nodeId) + is bool`.
- Не-`OperationCanceledException` в фоновом retry переводить в fail-fast `HardReset` + fail-safe `OpenGate()`.

### Don't

- Не использовать `ColumnExecutor.IsVisible`/`Status != null` как критерий idle/готовности новой карты.
- Не оставлять бесконечные ожидания без дедлайна и fail-safe выхода (исключение: execution retry-flow с `WaitForConnectionAsync(ct)`, где fail-safe обеспечен внешним PLC interrupt каскадом `PlcConnectionLost -> Reset -> Stop/Cancel`).
- Не проглатывать ошибки записи критичных PLC-тегов (`Reset`, `AskRepeat`, `End`).
- Не сохранять Bad-quality значения в runtime-кэш после rebind/reset.
- Не делать UI-блокировку без соответствующего runtime-gating в pipeline.
- Не включать авто-действия диагностики в ручных сценариях без явного контекста оператора.
- Не вводить дубли источников списка/сохранения параметров результата (single source of truth обязателен).
- Не менять reset/reconnect/error-flow точечно без проверки инвариантов completion и changeover.

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
- В execution retry-flow ожидание `WaitForConnectionAsync(ct)` не требует локального timeout: безопасность обеспечивается внешним каскадом `PlcConnectionLost -> Reset -> Stop/Cancel`.
- В pre-execution при потере OPC блокировать оба канала barcode (`BoilerInfo`, `BarcodeDebounceHandler`) и ставить scan-таймер на паузу.
- Для ECU error flow по ping: ошибка поднимается только в lock-контексте (`1005 in {1,2}` + whitelist), вне lock очищается.

## UI-инварианты (актуально)

- Единый стиль таблиц применять только через opt-in классы `grid-unified-host` + `grid-unified` из `wwwroot/css/app.css`.
- В родительских вкладках запрещены широкие `::deep .rz-data-grid*` override: shell/layout можно, типографику и геометрию таблицы — нельзя.
- Исключение зафиксировано: `Components/Main/TestSequenseGrid.razor` использует `main-grid-legacy` (компактный исторический вид главного экрана).
- Для любого DataGrid-профиля (`grid-unified`, `main-grid-legacy`, `overview-grid-io`, новые профили) использовать отдельный opt-in class в `wwwroot/css/app.css`; новый режим = новый класс.
- Заголовки DataGrid (включая anti-clipping) настраивать через `th` + внутренние контейнеры (`.rz-cell-data`, `.rz-column-title-content`, `.rz-sortable-column`, `.rz-column-title`).
- В каждом профиле раздельно настраивать header и body/edit (overflow/высота/типографика).
- Новые вкладочные контейнеры должны занимать всю высоту родителя (`display:flex`, `flex-direction:column`, `min-height:0`, `height:100%`), ориентир — `RecipesGrid`.
- `LogViewerTab` фиксирован: две вкладки (`Лог-файл`, `Время шагов`), `StepTimingsGrid` живёт во вкладке `Лог`.

## Stack

- Framework: .NET 10, WinForms + Blazor Hybrid
- UI: Radzen Blazor 8.3
- OPC-UA: OPCFoundation 1.5
- Modbus: NModbus 3.0
- Database: PostgreSQL + EF Core 10
- Logging: Serilog + DualLogger
- Excel: EPPlus 8.3
- Build: `dotnet build && dotnet run` (минимальная проверка — `dotnet build`)

## Архитектурный контур

`Program.cs -> Form1.cs (DI) -> BlazorWebView -> Radzen UI`; pipeline: `[Barcode] -> PreExecutionCoordinator -> TestExecutionCoordinator -> [OK/NOK]`; исполнение: `ScanSteps (pre-exec)` + `4 x ColumnExecutor`.

## Ключевые паттерны

- `DualLogger`: `logger.LogInformation("msg")` пишет одновременно в файл и UI теста.
- В тестовых шагах использовать pausable-сервисы (`PausableOpcUaTagService`, `PausableTagWaiter`, `PausableRegisterReader/Writer`).
- Системные операции — через не-pausable сервисы (`OpcUaTagService`, `RegisterReader/Writer`).
- В шагах использовать `context.DelayAsync()`, `context.DiagReader/Writer`.
- Primary constructors — стандарт: `public class MyStep(DualLogger<MyStep> logger, IOpcUaTagService tags) : ITestStep`.

## Правила кодирования

- Простота и читаемость важнее “умности”.
- Без overengineering и лишнего defense programming внутри внутреннего кода.
- КРИТИЧНО (не ослаблять): один основной управляющий блок на метод (`if/for/while/switch/try`), guard clauses допустимы.
- КРИТИЧНО (не ослаблять): метод около 50 строк; разрастание — сразу вынос в private helper-методы того же partial.
- `var` по умолчанию, `{}` обязательны.
- КРИТИЧНО (не ослаблять): сервисы до ~300 строк; при росте декомпозировать через `partial`, helper-классы и/или отдельные сервисы по ответственности.
- Именование: `PascalCase` для типов/методов, `camelCase` для локальных/параметров.
- Лимиты структуры (`~50 строк`, `1 основной управляющий блок`, `~300 строк на сервис`) обязательны для runtime-критичных веток и не пересматриваются без отдельного инцидента и обновления `AGENTS.md`.

## Контракт параметров результатов (обязательно)

- Имя параметра — каноническое и единое во всех местах (без вариантов написания).
- `isRanged = true` только если параметр реально имеет `min/max` по контракту потребителя.
- `isRanged = false` для строковых/информационных параметров без пределов.
- Метаданные (`isRanged`, единицы, границы) задавать по контракту каждого параметра отдельно; не копировать “по соседству”.
- Для групповых операций использовать единый источник списка (single source of truth), дубли вне списка удалять.
- Для retry перед повторной записью удалять старый результат (`testResultsService.Remove(...)`).

## Язык и кодировка

- Новые/переименованные комментарии и документация — на русском языке.
- Файлы в `UTF-8`; если файл уже с BOM, сохранять с BOM.
- При работе через CLI явно читать текст как UTF-8 (`Get-Content -Encoding UTF8`).
- Если появляются строки вида `РџР.../РѕР...` — исправлять в том же изменении и перечитывать файл в UTF-8.

## Документация

| Тема | Файл |
|------|------|
| State Management | `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md` |
| Error Handling | `Final_Test_Hybrid/Docs/runtime/ErrorCoordinatorGuide.md` |
| PLC Reset | `Final_Test_Hybrid/Docs/runtime/PlcResetGuide.md` |
| Steps | `Final_Test_Hybrid/Docs/execution/StepsGuide.md` |
| Cancellation | `Final_Test_Hybrid/Docs/execution/CancellationGuide.md` |
| Modbus | `Final_Test_Hybrid/Docs/diagnostics/DiagnosticGuide.md` |
| TagWaiter | `Final_Test_Hybrid/Docs/runtime/TagWaiterGuide.md` |
| Scanner | `Final_Test_Hybrid/Docs/diagnostics/ScannerGuide.md` |
| UI Index | `Final_Test_Hybrid/Docs/ui/README.md` |
| UI Principles | `Final_Test_Hybrid/Docs/ui/UiPrinciplesGuide.md` |
| UI Grids | `Final_Test_Hybrid/Docs/ui/GridProfilesGuide.md` |
| UI Buttons | `Final_Test_Hybrid/Docs/ui/ButtonPatternsGuide.md` |
| Main Screen UI | `Final_Test_Hybrid/Docs/ui/MainScreenGuide.md` |
| Settings Blocking UI | `Final_Test_Hybrid/Docs/ui/SettingsBlockingGuide.md` |

Детальная документация: `Final_Test_Hybrid/CLAUDE.md`.

## Периодичность и quality-gates

- После каждого значимого изменения в логике (reset/reconnect/error-flow) — минимум `dotnet build`.
- `jb inspectcode` запускать по изменённым `*.cs` после завершения логического блока (не после каждой косметики).
- Перед сдачей изменений обязательный чек-лист:
  1. `dotnet build Final_Test_Hybrid.slnx`
  2. `dotnet format analyzers --verify-no-changes`
  3. `dotnet format style --verify-no-changes`
- Точечный `inspectcode`: `jb inspectcode Final_Test_Hybrid.slnx "--include=<changed.cs;...>" --no-build --format=Text "--output=<path>" -e=WARNING`.

## Временные компромиссы

- Исторические кодировочные артефакты могут существовать локально; нормализация выполняется отдельной задачей.
- Для runtime OPC reconnect текущий компромисс: только полный rebuild, без гибридного ручного rebind.
- Отдельная ветка UI-текстов по `PlcConnectionLost` пока не вводится; используется текущая логика сообщений.
