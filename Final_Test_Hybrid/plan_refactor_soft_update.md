**Runtime Terminal Race Fix Package**

**Summary**
- Закрыть подтверждённые race в `completion` и `post-AskEnd`, где `GetValue<bool>()` превращает `missing/invalid cache` в ложный `false`.
- Закрыть связанный баг в `TagWaiter.WaitForFalseAsync`: после subscribe/resume пустой cache сейчас тоже может дать ложный `false`.
- Зафиксировать terminal ownership: `PLC loss` в `completion` и `post-AskEnd` всегда побеждает normal finish/cleanup и ведёт в `HardReset`.
- Сузить ownership `AutoReady`: `ON` снимает только `AutoModeDisabled`; `OFF` не создаёт `AutoModeDisabled` во время terminal handshake.
- Сохранить корректное ownership shared dispatcher в `ConnectionTestPanel`, не трогая глобально `IModbusDispatcher`.

**Key Changes**
- В [`OpcUaSubscription.Callbacks.cs`](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Services/OpcUa/Subscription/OpcUaSubscription.Callbacks.cs) добавить `TryGetValue<T>(string nodeId, out T value)`; `GetValue<T>` не менять.
- Перевести на known/unknown чтение только safety-critical decision-loop’и:
  - [`TestCompletionCoordinator.Flow.cs`](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.Flow.cs)
  - [`PreExecutionCoordinator.PostAskEnd.cs`](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.PostAskEnd.cs)
- Сохранить порядок PLC-решения без изменения: сначала `Req_Repeat=true`, потом `End=false` / `AskEnd=false`, иначе ждать.
- В [`TagWaiter.cs`](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Services/OpcUa/TagWaiter.cs) локально исправить `RecheckValue()` для `WaitForFalseAsync`: перепроверка после subscribe/resume должна использовать raw-cache семантику (`GetValue(nodeId)` + `is T`), а не `GetValue<T>`. `WaitGroup/WaitForAllTrue` в этот пакет не включать.
- Не расширять `ExecutionActivityTracker`. Добавить singleton `RuntimeTerminalState` с атомарными флагами `IsCompletionActive`, `IsPostAskEndActive`, `HasTerminalHandshake`.
- `TestCompletionCoordinator` и `PreExecutionCoordinator` обязаны обновлять `RuntimeTerminalState` в `try/finally`, finish и cancel paths.
- В `ErrorCoordinator` использовать `ActivityTracker.IsAnyActive || RuntimeTerminalState.HasTerminalHandshake` для `HandleConnectionChanged()`.
- В `ErrorCoordinator.HandleAutoReadyChanged()` не поднимать `AutoModeDisabled`, если активен terminal handshake. В `TryResumeFromPauseAsync()` резюмить только если `CurrentInterrupt == AutoModeDisabled`.
- `AutoReadySubscription` не менять; фильтрация ownership остаётся на coordinator layer.
- Для `_pendingExitReason` и `_resetSignal` сделать hardening без архитектурной переделки: атомарное хранение `_pendingExitReason`, local snapshot `_resetSignal`, единый resolver stop-reason до fallback.
- В [`ConnectionTestPanel.razor`](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Components/Overview/ConnectionTestPanel.razor) не вводить runtime deny для входа в панель и ручных действий; правка ограничена ownership shared dispatcher.
- `ConnectionTestPanel` должен хранить `startedByPanel`; в `DisposeAsync()` нельзя вызывать `StopAsync()`, если dispatcher был взят не панелью.

**Interface And Behavior Changes**
- Новый внутренний safe-read контракт: `OpcUaSubscription.TryGetValue<T>(...)`.
- Новый внутренний singleton: `RuntimeTerminalState`.
- Публичные UI/API сценарии не меняются; меняется внутренняя семантика safety-check’ов и interrupt ownership.
- Поведение UI-consumers на `CurrentInterrupt != null` изменится намеренно: во время terminal handshake исчезнет ложная блокировка `AutoModeDisabled` в `MessageService`, settings/QR UI и связанных read-only gates.

**Docs And Change Artifacts**
- Обновить source-of-truth:
  - [`PlcResetGuide.md`](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Docs/runtime/PlcResetGuide.md)
  - [`ErrorCoordinatorGuide.md`](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Docs/runtime/ErrorCoordinatorGuide.md)
  - [`StateManagementGuide.md`](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Docs/execution/StateManagementGuide.md)
  - [`ScanModeControllerGuide.md`](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Docs/runtime/ScanModeControllerGuide.md)
  - [`CycleExitGuide.md`](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Docs/execution/CycleExitGuide.md)
  - [`DiagnosticGuide.md`](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Docs/diagnostics/DiagnosticGuide.md)
  - [`TagWaiterGuide.md`](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Docs/runtime/TagWaiterGuide.md)
- Deferred scanner-ready описать как уже корректно реализованный `latest AutoReady semantics`, без runtime change.
- Обновить active impact [`2026-03-19-post-askend-reset-decision.md`](/D:/projects/Final_Test_Hybrid/Final_Test_Hybrid/Docs/impact/cross-cutting/2026-03-19-post-askend-reset-decision.md), а не создавать второй active cross-cutting файл.
- Текущее `no new incident` удалить; зафиксировать confirmed failure modes: stale-cache false-finish/false-cleanup, `WaitForFalseAsync` false-success, shared dispatcher ownership в `ConnectionTestPanel`.
- Так как отдельного incident-контура в `Docs` нет, создать `openspec/changes/fix-runtime-terminal-race-package/` и сослаться на него из impact.

**Test Plan**
- Добавить новый test project.
- `completion`: `unknown` cache не даёт `Finished`; кейс `OK/NOK image shown -> PLC loss -> HardReset`.
- `post-AskEnd`: `unknown` cache не даёт normal cleanup; `PLC loss` всегда уходит в `HardReset`.
- `TagWaiter.WaitForFalseAsync`: empty cache после subscribe/resume не завершает ожидание до реального `false`.
- Downstream регрессии `WaitForFalseAsync`: как минимум `BlockBoilerAdapterStep` и `ColumnExecutor` не стартуют раньше времени на missing cache.
- `AutoReady OFF` во время `completion/post-AskEnd` не создаёт `AutoModeDisabled`.
- `AutoReady ON` не снимает `BoilerLock`, `PlcConnectionLost`, `TagTimeout` и любой non-`AutoModeDisabled` interrupt.
- Deferred scanner-ready: `true->false`, `false->true`, `repeat outcome`.
- Pre-start окно вокруг `_pendingExitReason/_resetSignal`: без случайного `PipelineCancelled`.
- `ConnectionTestPanel.DisposeAsync()` панели не гасит shared dispatcher, если dispatcher стартовала не панель.
- Verification после реализации:
  - `dotnet build Final_Test_Hybrid.slnx`
  - `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx`
  - `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx`
  - `jb inspectcode ... -e=WARNING`
  - `jb inspectcode ... -e=HINT`
  - `openspec validate fix-runtime-terminal-race-package --strict --no-interactive`

**Assumptions And Residual Risks**
- Массовую миграцию всех `GetValue<bool>` не делать.
- `ExecutionActivityTracker` не расширять; terminal windows живут в отдельном `RuntimeTerminalState`.
- `WaitGroup/WaitForAllTrue` остаются вне пакета.
- Manual screens (`HandProgram`, `IoEditorDialog`, `AiCallCheck`, `PidRegulatorCheck`, `RtdCalCheck`) поведением в этом пакете не меняются.
- Timer semantics не менять.
- Текущая сборка уже проходит; известный baseline warning только `MSB3277` по `WindowsBase`.




Да. Ниже именно целевое поведение после этого fix-package, по сценариям. Это не новый state machine, а уточнение и ужесточение текущих контрактов.

По Фазам

Idle / до логина / без активного цикла: поведение почти не меняется. Потеря PLC тут не превращается в fake completion, система просто остаётся в disconnected/idle состоянии.
Ожидание штрихкода: как и сейчас, сканирование зависит от AutoReady и связи. Если AutoReady OFF, новый цикл не стартует.
Pre-execution / active test: текущее pause-поведение сохраняется. AutoReady OFF тут по-прежнему может поднимать AutoModeDisabled, а AutoReady ON по-прежнему может снимать только этот interrupt.
Completion window: после показа картинки OK/NOK тест ещё не считается завершённым. Картинка не равна подтверждённому концу цикла.
Post-AskEnd window: это тоже terminal window. До финального PLC-решения normal cleanup не считается подтверждённым.
Что Будет В Critical Cases

Картинка успеха уже показана, потом пропал PLC: normal finish больше не выигрывает. Будет HardReset, а не Finished.
Completion / post-AskEnd читает пустой cache: пустой cache больше не трактуется как валидный false. Цикл ждёт реальное PLC-значение или reset/cancel.
PLC loss во время post-AskEnd: больше не выпадет из owner-detection. Уйдёт в HardReset.
AutoReady OFF во время completion/post-AskEnd: не создаёт AutoModeDisabled. Terminal handshake не должен получать чужой interrupt.
AutoReady ON во время BoilerLock: ничего не резюмится. BoilerLock живёт по своим правилам.
OK/NOK repeat: repeat-first приоритет сохраняется. Если PLC реально дал repeat, он остаётся repeat.
Deferred scanner-ready после post-AskEnd: остаётся как сейчас в коде. Финальное ready решается по последнему AutoReady на момент закрытия post-AskEnd, не по старому snapshot.
Граничные И Неприятные Случаи

Reconnect / bad-quality / rebuild подписки: empty cache после rebuild больше не должен давать false-finish/false-cleanup.
Узкое окно reset/cancel до старта цикла: exit reason должен стать детерминированным, без случайного PipelineCancelled.
WaitForFalseAsync: это тоже исправляется. Иначе шаги могли бы раньше времени считать, что PLC уже сбросил End/Error/TestEndStep.
BlockBoilerAdapterStep / ColumnExecutor / skip cleanup / retry reset: после фикса они не должны проходить вперёд на пустом кеше.
Закрытие ConnectionTest: не должно гасить shared ModbusDispatcher, если им уже владеет runtime.
Что Будет С UI И Блокировками

Ложный AutoModeDisabled в terminal window уйдёт.
Значит часть UI, которая сейчас смотрит на CurrentInterrupt != null, перестанет получать лишнюю блокировку именно в этом сценарии.
Но реальные блокировки от reset, post-AskEnd, BoilerLock, TagTimeout, PLC loss сохраняются.
Таймеры

Changeover timer: по пакету не меняется. Логика старта/остановки остаётся текущей.
Test timer: не меняется.
Scan timer: reset-vs-resume не переделывается. Сохраняется текущая deferred semantics после post-AskEnd.
Step timers: глобально не переделываются.
Важный нюанс: после фикса WaitForFalseAsync некоторые шаги могут визуально стать длиннее. Это не новая логика таймеров, а исчезновение ложного раннего “PLC уже сбросил сигнал”.
Что Не Меняется

Разделение PLC reset и HardReset.
Приоритет reset над completion.
Save-retry в completion.
Full rebuild runtime subscription после reconnect.
Общая changeover-архитектура.
Общая pause-модель активного pre-execution/test execution.
Что Остаётся Вне Scope Этого Пакета

IoEditorDialog.
AiCallCheck, PidRegulatorCheck, RtdCalCheck.
WaitGroup/WaitForAllTrue массово не переписываются.
Полный рефактор execution state machine.
Если хочешь, следующим сообщением я могу дать это же в виде очень жёсткой таблицы:
сценарий -> до фикса -> после фикса -> какие файлы отвечают -> риск регрессии.




Ниже жёсткая матрица: сценарий -> сейчас/риск -> после пакета -> какие места отвечают -> риск регрессии.

Lifecycle / Runtime

Сценарий	Сейчас / риск	После пакета	Ключевые места
Idle, оператор не в цикле	Почти без изменений; PLC loss не должен ничего “завершать”	Остаётся idle/disconnected	ErrorCoordinator.cs, ScanModeController.cs
Ожидание штрихкода, AutoReady OFF	Scan mode деактивируется как сейчас	Не меняется	ScanModeController.cs
Ожидание штрихкода, PLC loss	Вход/скан уже блокируются текущей логикой	Не меняется по смыслу	BarcodeDebounceHandler.cs, ScanModeController.cs
Active pre-execution, AutoReady OFF	Поднимается AutoModeDisabled, пауза допустима	Остаётся так же	ErrorCoordinator.cs, PreExecutionCoordinator.Pipeline.cs
Active test, AutoReady OFF	Pause-path допустим, idle-timeout знает про AutoModeDisabled	Остаётся так же	TestExecutionCoordinator.IdleTimeout.cs, ErrorCoordinator.cs
BoilerLock, потом AutoReady ON	Сейчас может снять чужой paused interrupt	Больше не снимает BoilerLock	ErrorCoordinator.Resolution.cs, BoilerLockRuntimeService.cs
Показана картинка OK/NOK, потом пропал PLC	Самый опасный сценарий: возможен ложный Finished	Всегда выигрывает HardReset; картинка не считается подтверждением конца теста	PreExecutionCoordinator.MainLoop.cs, TestCompletionCoordinator.Flow.cs
completion видит empty/invalid cache	unknown может стать ложным End=false	unknown больше не решение PLC; ждём valid value или reset/cancel	OpcUaSubscription.Callbacks.cs, TestCompletionCoordinator.Flow.cs
post-AskEnd, PLC loss	Сейчас ownership дырявый: может не дойти до HardReset вовремя	Всегда уходит в HardReset	PreExecutionCoordinator.PostAskEnd.cs, PreExecutionCoordinator.Subscriptions.cs, ErrorCoordinator.cs
post-AskEnd видит empty/invalid cache	Сейчас возможен ложный normal cleanup	unknown больше не трактуется как AskEnd=false	OpcUaSubscription.Callbacks.cs, PreExecutionCoordinator.PostAskEnd.cs
AutoReady OFF во время completion/post-AskEnd	Сейчас может загрязнить CurrentInterrupt	Не создаёт AutoModeDisabled в terminal window	ErrorCoordinator.cs
AutoReady ON в terminal window	Сейчас может сделать лишний resume	Resume только для AutoModeDisabled	ErrorCoordinator.Resolution.cs
OK repeat / NOK repeat	Базовая семантика repeat-first уже есть	Сохраняется без изменения	TestCompletionCoordinator.Flow.cs, PreExecutionCoordinator.PostAskEnd.cs
OnResetCompleted, потом AutoReady меняется до конца post-AskEnd	Код уже берёт latest state	Не меняется; только docs/tests	ScanModeController.cs
Repeat outcome после post-AskEnd	Scanner-ready не должен подниматься	Остаётся так же	ScanModeController.cs, PreExecutionCoordinator.Changeover.cs
Узкое окно pre-start reset/cancel	Сейчас возможен случайный PipelineCancelled	Exit reason должен стать детерминированным	PreExecutionCoordinator.MainLoop.cs, PreExecutionCoordinator.StopReason.cs
Shared Helpers / Hidden Blast Radius

Сценарий	Сейчас / риск	После пакета	Ключевые места
WaitForFalseAsync после subscribe на пустом cache	Может мгновенно “успешно” завершиться	Ждёт реальный false, а не default(bool)	TagWaiter.cs, OpcUaSubscription.Callbacks.cs
ColumnExecutor ждёт сброс End перед шагом	Следующий шаг может стартовать раньше реального PLC-reset	Больше не стартует на пустом кеше	ColumnExecutor.cs
BlockBoilerAdapterStep ждёт End=false	Pre-execution может пойти дальше раньше времени	Ждёт реальный сброс	BlockBoilerAdapterStep.cs
Retry/skip cleanup ждёт сброс PLC сигналов	UI/flow может считать cleanup завершённым раньше PLC	Cleanup идёт только по реальному reset сигналов	TestExecutionCoordinator.PlcErrorSignals.cs
Req_Repeat reset waiter	Может раньше времени считать repeat сброшенным	Ждёт реальный сброс	ErrorCoordinator.Resolution.cs
Stale AskEnd sync в PLC reset	Пустой subscription cache может ослабить stale-protection	Остаётся только реальный false	PlcResetCoordinator.cs
Diagnostics / Shared Dispatcher

Сценарий	Сейчас / риск	После пакета	Ключевые места
Закрытие ConnectionTest	Сейчас DisposeAsync() может остановить dispatcher runtime’а	StopAsync() только если dispatcher стартовала сама панель	ConnectionTestPanel.razor, ModbusDispatcher.cs, CheckCommsStep.cs
Manual screens / write-path'ы	Текущее поведение сохраняется	В этом пакете не меняются	HandProgramDialog.razor, IoEditorDialog.razor.cs, AiCallCheck.razor.cs, PidRegulatorCheck.razor.cs, RtdCalCheck.razor.cs
UI / Messages / Settings

Сценарий	Сейчас / риск	После пакета	Ключевые места
Settings/engineering screens видят ложный AutoModeDisabled в terminal window	Лишняя блокировка и неверная причина на UI	Ложный interrupt уйдёт; реальные reset/post-AskEnd блокировки останутся	MainEngineering.razor.cs, AdminAuthorizationQr.razor.cs, SwitchMes.razor.cs
Main message при AutoReady OFF во время post-AskEnd	Сообщение всё ещё может говорить “Нет автомата. Выполняется сброс...”	Это останется, пока активен post-AskEnd; это отдельный UI-impact, не runtime bug	MessageService.cs
BoilerLock recovery	Сейчас может быть сорван broad AutoReady ON -> resume	Ownership становится корректным	BoilerLockRuntimeService.cs, ErrorCoordinator.Resolution.cs
Таймеры

Таймер / механизм	Что будет после пакета	Что не меняется	Ключевые места
Step timers	Логика не меняется, но исчезнут ложные ранние старты/cleanup, поэтому время станет “честнее”	Глобальной переделки нет	ColumnExecutor.cs, BlockBoilerAdapterStep.cs
Test timer	Не меняется	Остаётся текущая семантика	PreExecutionCoordinator.MainLoop.cs
Scan timer	Не меняется по reset-vs-resume	Deferred ready остаётся как сейчас	ScanModeController.cs
Changeover	Не меняется как отдельная семантика	Пакет не трогает changeover-модель	PreExecutionCoordinator.Changeover.cs
Completion image	Не таймер, но важный маркер: картинка больше не считается подтверждением завершения	Показывается как сейчас, но reset/loss её побеждает	PreExecutionCoordinator.MainLoop.cs, TestCompletionUiState.cs







Ниже в жёстком формате: инвариант -> что обязано сохраниться -> что меняется -> что точно вне пакета.

1. Reset Всегда Старше Completion

Инвариант:
PLC reset и HardReset остаются отдельными потоками, и в гонке reset всегда старше normal finish.
Что обязано сохраниться:
Ни completion, ни post-AskEnd не могут “додавить” успешное завершение поверх потери PLC или reset.
Что меняется:
В TestCompletionCoordinator.Flow.cs и PreExecutionCoordinator.PostAskEnd.cs пустой cache больше не считается PLC-решением.
Что вне пакета:
Полная переделка reset choreography, delay policy и общей reset architecture.
2. Картинка Успеха Не Равна Завершённому Тесту

Инвариант:
Показ OK/NOK на экране не является PLC-подтверждением конца цикла.
Что обязано сохраниться:
Картинка может показываться как сейчас, но при PLC loss/reset после её показа система обязана уйти в reset-path, а не в normal finish.
Что меняется:
Сценарий из PreExecutionCoordinator.MainLoop.cs + TestCompletionCoordinator.Flow.cs фиксируется явно как terminal race window.
Что вне пакета:
Переработка UI completion screen, текстов, анимаций, общей UX-логики экрана результата.
3. Unknown Не Может Превращаться В False В Критичных Решениях

Инвариант:
PLC-решения в safety-critical loop нельзя принимать по default(bool).
Что обязано сохраниться:
Req_Repeat=true и End=false / AskEnd=false по-прежнему означают те же PLC-решения, если значение реально известно.
Что меняется:
В OpcUaSubscription.Callbacks.cs добавляется TryGetValue<T>, и только критичные loop’и уходят с GetValue<bool>() на known/unknown чтение.
Что вне пакета:
Массовая миграция всех мест в репозитории с GetValue<bool>().
4. Completion И Post-AskEnd Это Terminal Windows

Инвариант:
completion и post-AskEnd считаются terminal PLC-handshake окнами, а не обычной active-test паузой.
Что обязано сохраниться:
Эти окна должны доживать до PLC-решения или reset/cancel; их нельзя ломать чужим interrupt ownership.
Что меняется:
Добавляется отдельное runtime-состояние terminal window, а не расширение ExecutionActivityTracker.cs.
Что вне пакета:
Полный рефактор state machine и объединение всех execution phases в один универсальный tracker.
5. PLC Loss В Post-AskEnd Обязан Вести В HardReset

Инвариант:
Потеря PLC в post-AskEnd не может заканчиваться обычным cleanup.
Что обязано сохраниться:
Потеря связи в terminal window должна детерминированно захватываться error/reset owner.
Что меняется:
ErrorCoordinator.cs начинает учитывать terminal state, а не только ActivityTracker.IsAnyActive.
Что вне пакета:
Глобальная смена политики PlcConnectionLostBehavior, задержек и reconnect semantics.
6. AutoReady ON Снимает Только Свой Interrupt

Инвариант:
AutoReady ON имеет право восстанавливать только AutoModeDisabled.
Что обязано сохраниться:
BoilerLock, PlcConnectionLost, TagTimeout и другие interrupt’ы не должны сниматься “широким resume”.
Что меняется:
В ErrorCoordinator.Resolution.cs TryResumeFromPauseAsync() становится ownership-aware.
Что вне пакета:
Полная переработка pause subsystem и общего interrupt state model.
7. AutoReady OFF Не Должен Загрязнять Terminal Window

Инвариант:
AutoReady OFF не должен создавать AutoModeDisabled во время completion/post-AskEnd.
Что обязано сохраниться:
В обычном active pre-execution/test execution AutoReady OFF продолжает работать как сейчас.
Что меняется:
Только terminal windows исключаются из этого ownership-path в ErrorCoordinator.cs.
Что вне пакета:
Глобальное изменение pause semantics активного теста.
8. Deferred Scanner-Ready Остаётся Как Сейчас

Инвариант:
После OnResetCompleted, если post-AskEnd ещё идёт, итоговый scanner-ready решается по latest AutoReady на момент закрытия post-AskEnd.
Что обязано сохраниться:
true->false, false->true, repeat outcome должны работать как сейчас.
Что меняется:
Код не меняется; обновляются docs и тесты.
Что вне пакета:
Любая смена reset-vs-resume semantics scan timer или scanner lifecycle.
Код:
ScanModeController.cs
9. WaitForFalseAsync Не Должен Ложно Завершаться На Пустом Cache

Инвариант:
Ожидание false должно завершаться только по реальному известному false.
Что обязано сохраниться:
Первичная cache-проверка через raw value остаётся корректной и быстрой.
Что меняется:
В TagWaiter.cs post-subscribe recheck для WaitForFalseAsync перестаёт использовать GetValue<bool>().
Что вне пакета:
Массовая переделка WaitGroup и всех generic wait-path’ов.
10. Downstream Step Flow Должен Стать Честнее, Но Не Другим По Смыслу

Инвариант:
Шаги, которые ждут сброс PLC-сигналов, должны стартовать только после реального сброса.
Что обязано сохраниться:
Бизнес-семантика шагов и их порядок не меняются.
Что меняется:
Исчезают ложные ранние проходы из-за пустого cache в ColumnExecutor.cs, BlockBoilerAdapterStep.cs, TestExecutionCoordinator.PlcErrorSignals.cs, PlcResetCoordinator.cs.
Что вне пакета:
Переписывание шагов, их timeouts, retry policy и sequencing logic.
11. Таймеры Не Переделываются

Инвариант:
changeover, test timer, scan timer, step timers глобально не меняются этим пакетом.
Что обязано сохраниться:
Existing timer semantics и их ownership не должны сломаться из-за race fix.
Что меняется:
Только исчезнут ложные ранние переходы; поэтому некоторые шаги визуально могут длиться чуть дольше, но это честное ожидание PLC, а не новая timer policy.
Что вне пакета:
Любой рефактор таймеров.
Кодовые контуры:
PreExecutionCoordinator.Changeover.cs, ScanModeController.cs, ColumnExecutor.cs
12. Shared ModbusDispatcher Нельзя Ломать Панелью ConnectionTest

Инвариант:
Manual panel не должна останавливать dispatcher, если не она его стартовала.
Что обязано сохраниться:
Runtime-потребители dispatcher продолжают работать как сейчас.
Что меняется:
В ConnectionTestPanel.razor добавляется ownership startedByPanel.
Что вне пакета:
Перевод dispatcher на отдельные экземпляры, scoped lifetime или большую переработку diagnostic transport.
Связанные места:
ModbusDispatcher.cs, CheckCommsStep.cs
13. CurrentInterrupt Consumers Изменятся Только Там, Где Это Намеренно

Инвариант:
Реальные блокировки по reset, PLC-loss, BoilerLock и другим чужим interrupt’ам остаются.
Что обязано сохраниться:
UI не должен внезапно стать “слишком свободным”.
Что меняется:
Исчезнет ложный AutoModeDisabled в terminal window для части UI-consumers.
Что вне пакета:
Переделка MessageService.cs и общей UX-матрицы сообщений.
14. Docs Должны Догнать Код, А Не Наоборот

Инвариант:
Stable docs и impact обязаны быть синхронизированы с фактическим runtime behavior.
Что обязано сохраниться:
Source-of-truth остаётся в Docs/*Guide.md, impact остаётся историей изменений.
Что меняется:
Нужно обновить:
PlcResetGuide.md,
ErrorCoordinatorGuide.md,
StateManagementGuide.md,
ScanModeControllerGuide.md,
CycleExitGuide.md,
DiagnosticGuide.md,
TagWaiterGuide.md
Что вне пакета:
Массовая чистка всей исторической документации вне релевантного контура.
15. Incident/Change След Должен Быть Явным

Инвариант:
Новый confirmed failure mode нельзя оставлять только “в голове” или только в коде.
Что обязано сохраниться:
Если это новый failure mode, он должен появиться в incident/change documentation и в impact.
Что меняется:
Active impact 2026-03-19-post-askend-reset-decision.md должен быть обновлён, плюс нужен явный change-set/incident trail.
Что вне пакета:
Изобретение новой process-схемы вместо существующих правил repo.
Если нужно, следующим сообщением соберу это в финальный “контракт пакета” на 10-12 коротких пунктов, уже без пояснений, как базовый approval checklist перед реализацией.
