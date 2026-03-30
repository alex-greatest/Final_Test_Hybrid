# 2026-03-24 logviewer-tab-tail-refresh-and-active-gate

## Контур

- UI / вкладка `Лог`

## Что изменено

- `MyComponent` теперь явно активирует `LogViewerTab` через `@ref` и `OnMainTabChanged`, чтобы компонент не выполнял фоновые refresh вне активной вкладки и не зависел от lifecycle-поведения `RadzenTabs` при `RenderMode.Client`.
- `LogViewerTab` переведён с `ReadToEndAsync() + Split(...)` на локальный bounded tail-reader:
  - первичная загрузка читает только хвост файла;
  - повторные refresh читают только новые байты от последней позиции;
  - в UI хранится bounded buffer последних `10_000` строк.
- При скрытой вкладке `Лог` автообновление останавливается; при возврате на вкладку `MyComponent` выполняет явную активацию компонента и немедленный refresh.
- Для сценариев `new session`, truncate и повторного появления файла reader-state сбрасывается локально внутри `Components/Logs/*`.
- После follow-up ревью добавлены локальные hardening-правки:
  - `LogViewerTab` больше не рендерит после `Dispose()` и не держит polling вне активной вкладки;
  - активация/деактивация log-viewer переведена с parameter-driven lifecycle на явный вызов `SetActiveAsync(...)`, потому что parameter gate дал регрессию `Лог пуст` на живом session-файле;
  - snapshot включает незавершённую последнюю строку, чтобы не было ложного `Лог пуст`;
  - начальный tail не отрезает первую полноценную строку, если offset попал ровно на границу строки;
  - same-path recreate определяется по `CreationTimeUtc`, чтобы не терять старт нового файла;
  - reader открывает файл с `FileShare.Delete`, чтобы не мешать rollover через rename/delete.
- Ошибки чтения файла больше не пробрасываются в UI-поток: вкладка показывает сообщение об ошибке, а `LogViewerTab` пишет локальный error/warning-log в общий app-log.

## Что сознательно не менялось

- `DualLogger`, `ITestStepLogger`, `TestStepLogger`.
- `Form1`, DI, runtime pipeline, OPC, Modbus, reset/reconnect, execution services.
- Формат teststep-log файла и место его записи.

## Затронутые файлы

- `Final_Test_Hybrid/MyComponent.razor`
- `Final_Test_Hybrid/Components/Logs/LogViewerTab.razor`
- `Final_Test_Hybrid/Components/Logs/LogViewerTab.razor.cs`
- `Final_Test_Hybrid/Components/Logs/LogFileTailReader.cs`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остались baseline warnings `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0`, не связанные с change-set.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Logs/LogViewerTab.razor.cs;Final_Test_Hybrid/Components/Logs/LogFileTailReader.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\inspect-logviewer-warning.txt" -e=WARNING` — отчёт пуст (`Solution Final_Test_Hybrid.slnx`).
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Logs/LogViewerTab.razor.cs;Final_Test_Hybrid/Components/Logs/LogFileTailReader.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\inspect-logviewer-hint.txt" -e=HINT` — отчёт пуст (`Solution Final_Test_Hybrid.slnx`).
- `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\localization-sync-guard\scripts\replay_localization_sync.ps1 -RepoRoot . -RequireResourceSync` — успешно.

## Residual Risks

- Desktop-runtime smoke на реально длинном файле вручную не выполнялся; корректность tail-reading подтверждена кодом, сборкой и статическими проверками, но не интерактивным прогоном UI.
- Tail-reader по-прежнему декодирует UTF-8 кусками без отдельного decoder-state; для текущего line-based Serilog writer риск минимален, но теоретически на границе многобайтного символа возможен артефакт отображения.

## Инциденты

- no new incident
