# 2026-03-19 interrupt-reason-dialog-danger-cancel-button

## Контур

- UI / Main screen / Interrupt dialog

## Что изменено

- В `Components/Main/Modals/Interrupt/InterruptReasonDialog.razor` кнопки переставлены в порядок `Сохранить` слева, `Отмена` справа.
- Кнопка `Отмена` переведена на `ButtonStyle.Danger`, сохранив иконку `close` и блокировку через `_isSaving`.
- Для `Отмена` добавлена инженерная парольная защита: перед закрытием interrupt-диалога открывается `Components/Engineer/Modals/PasswordDialog.razor`, который проверяет тот же `AppSettingsService.EngineerPassword`, что и инженерные кнопки в `MainEngineering`.
- В `Components/Main/Modals/Interrupt/InterruptReasonDialog.razor.css` общая типографика и геометрия кнопок вынесены в общий локальный класс `dialog-button`.
- Кнопки `Сохранить` и `Отмена` теперь используют одинаковые `padding`, `font-size`, `font-weight`, `border-radius` и минимальную высоту, чтобы визуальный масштаб был согласован.
- В `Docs/ui/ButtonPatternsGuide.md` зафиксировано узкое исключение: для interrupt-диалога кнопка `Отмена` оформляется как `Danger` и закрывает диалог только после успешной проверки инженерного пароля, хотя базовая семантика кнопок отмены в системе остаётся `Light`.

## Затронутые файлы

- `Final_Test_Hybrid/Components/Main/Modals/Interrupt/InterruptReasonDialog.razor`
- `Final_Test_Hybrid/Components/Main/Modals/Interrupt/InterruptReasonDialog.razor.css`
- `Final_Test_Hybrid/Docs/ui/ButtonPatternsGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — не выполнен: сборка остановилась на `MSB3027/MSB3021`, потому что `bin\Debug\net10.0-windows\Final_Test_Hybrid.exe` заблокирован запущенным процессом `Final_Test_Hybrid (PID 38900)`; внешний warning `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0` сохранился и к этой правке не относится.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Main/Modals/Interrupt/InterruptReasonDialog.razor" --no-build --format=Text "--output=inspect-warning-interrupt-dialog.txt" -e=WARNING` — успешно; warning по отчёту нет.

## Инциденты

- no new incident
