# 2026-03-20 boiler-info-preserved-enter-after-soft-reset

## Контур

- UI / Main screen / BoilerInfo / manual barcode resend after soft reset

## Что изменено

- В `BoilerInfo.razor` убран split между отображаемым текстом поля и `_manualInput`:
  - поле серийного номера теперь работает через единый локальный draft;
  - `Enter` отправляет тот же draft, который сейчас виден оператору.
- Для синхронизации preserved barcode добавлен internal helper `BoilerInfoInputDraft`:
  - preserved значение читается в прежнем приоритете `BoilerState.SerialNumber -> PreExecution.CurrentBarcode`;
  - draft обновляется только когда preserved значение реально изменилось;
  - обычные `StateHasChanged`/gating refresh больше не возвращают поле к старому barcode поверх ручной правки.
- После soft reset preserved barcode в `BoilerInfo` снова считается активным содержимым поля:
  - если поле вернулось в editable состояние, `Enter` повторно отправляет сохранённый barcode без обязательного удаления или добавления символа;
  - если оператор вручную очистил поле, submit не падает обратно в старое preserved значение.
- Кнопка очистки в `BoilerInfo` оставлена предсказуемой:
  - очищается локальный draft;
  - вызываются `PreExecution.ClearBarcode()` и `BoilerState.Clear()`.
- В `ScannerGuide.md` зафиксирован новый source-of-truth для ручного ввода после soft reset.
- Добавлен xUnit coverage на draft-sync helper:
  - первичная синхронизация preserved значения;
  - защита ручной правки от случайного overwrite при неизменном preserved значении;
  - отсутствие fallback в старый barcode после ручной очистки поля;
  - обновление draft при приходе нового preserved значения.

## Затронутые файлы

- `Final_Test_Hybrid/Components/Main/BoilerInfo.razor`
- `Final_Test_Hybrid/Components/Main/BoilerInfoInputDraft.cs`
- `Final_Test_Hybrid.Tests/Runtime/BoilerInfoInputDraftTests.cs`
- `Final_Test_Hybrid/Docs/diagnostics/ScannerGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остались baseline warning `MSB3277` по конфликту `WindowsBase` и существующий `CS0067` в `DiagnosticDispatcherOwnershipTests.TestModbusDispatcher.PingDataUpdated`, не связаны с этой правкой.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj` — успешно, `35/35`.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Main/BoilerInfoInputDraft.cs;Final_Test_Hybrid.Tests/Runtime/BoilerInfoInputDraftTests.cs" --no-build --format=Text "--output=C:\Users\Alexander\.codex\worktrees\a9be\Final_Test_Hybrid\inspect-warning-boiler-info-input-draft.txt" -e=WARNING` — отчёт пуст (`Solution Final_Test_Hybrid.slnx`).

## Residual Risks

- В этой сессии не выполнялся интерактивный прогон WinForms + Blazor Hybrid UI, поэтому сценарий `MES -> окно ошибки -> soft reset -> повторный Enter без правки` подтверждён кодом и автопроверками, но не ручным окном приложения.

## Инциденты

- no new incident
