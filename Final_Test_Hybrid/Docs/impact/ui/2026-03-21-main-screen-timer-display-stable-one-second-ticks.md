# 2026-03-21 main-screen-timer-display-stable-one-second-ticks

## Контур

- UI / Main screen / TestTimerDisplay / ChangeoverTimerDisplay / BoilerState timer snapshot

## Что изменено

- В `TestTimerDisplay.razor` и `ChangeoverTimerDisplay.razor` отображение времени отвязано от редких `On*TimerTick` как единственного источника перерисовки UI.
- В оба компонента добавлен локальный `PeriodicTimer` с интервалом `250 ms`:
  - цикл регулярно перечитывает форматированное время из `BoilerState`;
  - `StateHasChanged()` вызывается только если строка `HH:mm:ss` реально изменилась.
- События `BoilerState.OnTestTimerTick`, `BoilerState.OnChangeoverTimerTick` и `BoilerState.OnCleared` сохранены как немедленный сигнал обновления, но визуальная стабильность больше не зависит только от частоты их доставки.
- Компоненты переведены на `IAsyncDisposable`, чтобы корректно остановить локальный refresh-loop при удалении из UI.
- После ручной проверки сценария `result-image -> ожидание AskEnd=false или Req_Repeat` выявилась регрессия:
  - локальный polling честно продолжал читать растущее время из `BoilerState`;
  - `BoilerState.StopTestTimer()` останавливал только внутренний `System.Threading.Timer`, но не фиксировал test duration.
- Root-cause fix внесён в `BoilerState`:
  - добавлен snapshot `stopped duration` для тестового таймера по аналогии с уже существующей семантикой changeover-таймера;
  - `StopTestTimer()` теперь фиксирует накопленную длительность один раз;
  - `GetTestDuration()` после остановки возвращает frozen value до следующего `StartTestTimer()` или `Clear()`.
- Добавлен xUnit regression test, который подтверждает, что после `StopTestTimer()` значение больше не растёт.

## Зачем

- Устранить визуальный дефект, при котором оператор иногда видел скачок таймера сразу на `+2` секунды.
- Сохранить `BoilerState` как source of truth для отображения.
- Исправить скрытую несогласованность source-of-truth: остановка тестового таймера должна замораживать duration, а не только прекращать события тика.

## Затронутые файлы

- `Final_Test_Hybrid/Components/Main/TestTimerDisplay.razor`
- `Final_Test_Hybrid/Components/Main/ChangeoverTimerDisplay.razor`
- `Final_Test_Hybrid/Models/BoilerState.cs`
- `Final_Test_Hybrid.Tests/Runtime/BoilerStateTests.cs`

## Проверки

- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --filter BoilerStateTests` — успешно, `1/1`.
- `dotnet build Final_Test_Hybrid.slnx` — успешно; остались baseline warning `MSB3277` по конфликту `WindowsBase`, не связаны с этой правкой.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Main/TestTimerDisplay.razor;Final_Test_Hybrid/Components/Main/ChangeoverTimerDisplay.razor" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\inspect-warning-main-timers.txt" -e=WARNING` — отчёт пуст (`Solution Final_Test_Hybrid.slnx`).
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/BoilerState.cs;Final_Test_Hybrid.Tests/Runtime/BoilerStateTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\inspect-warning-boiler-state.txt" -e=WARNING` — отчёт пуст (`Solution Final_Test_Hybrid.slnx`).
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/BoilerState.cs;Final_Test_Hybrid.Tests/Runtime/BoilerStateTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\inspect-hint-boiler-state.txt" -e=HINT` — есть существующие hints про неиспользуемые члены `BoilerState` (`IsValid`, `TestResult`, `StartChangeoverTimer`); новых подсказок по внесённой логике заморозки таймера нет.

## Residual Risks

- Интерактивный WinForms + Blazor прогон в этой сессии не выполнялся, поэтому сценарий `completion image -> ожидание PLC decision` подтверждён кодом и автотестом, но не ручной визуальной валидацией на стенде.
- При тяжёлой общей блокировке UI-потока дольше секунды локальный polling тоже задержится; текущая правка устраняет пропуски из-за рассинхронизации событий, но не может обойти полную заморозку UI.

## Инциденты

- no new incident
