# 2026-04-16 app-icon-window-and-desktop-shortcut

## Контур

- UI / WinForms shell / `Form1`
- Сборка приложения / иконка `exe`
- Рабочий стол / ярлык `Final Test.lnk`

## Что изменено

- В проект добавлен новый `Final_Test_Hybrid/App.ico`, собранный из пользовательского `E:\7776_96.png`.
- В `Final_Test_Hybrid.csproj` добавлен `ApplicationIcon`, чтобы `exe` и связанные ярлыки брали иконку из проекта.
- В `Form1.cs` окно теперь назначает иконку через `Icon.ExtractAssociatedIcon(Application.ExecutablePath)`, чтобы заголовок окна использовал ту же иконку, что и `exe`.
- После обратной связи пользователя иконка была перегенерирована с более плотным заполнением без лишних прозрачных полей, чтобы визуально выглядеть крупнее в заголовке окна и на ярлыке.
- По повторной просьбе пользователя scale содержимого внутри `App.ico` и `Final_Test_Hybrid.Shortcut.ico` дополнительно увеличен через overscale (`fit * 1.18`), чтобы сам объект внутри ярлыка выглядел ещё крупнее без системного увеличения размера desktop icons.
- На рабочем столе создан ярлык `C:\Users\Alexander\Desktop\Final Test.lnk`, указывающий на `D:\projects\Final_Test_Hybrid\Final_Test_Hybrid\bin\Debug\net10.0-windows\Final_Test_Hybrid.exe`.
- Для ярлыка назначен отдельный `Final_Test_Hybrid.Shortcut.ico` в output-папке, синхронизированный по визуальному образу с `App.ico`.

## Что сознательно не менялось

- Runtime pipeline, reset/reconnect/error-flow и Blazor UI-компоненты.
- Размер системного значка в caption/title bar: Windows задаёт его сама, поэтому изменён только визуальный scale содержимого внутри `ico`.
- Реальный размер ярлыка на рабочем столе: Windows по-прежнему задаёт его системно; change-set меняет только размер изображения внутри ячейки значка.
- Stable UI docs: новый UI-контракт или новый экран не вводились.

## Затронутые файлы

- `Final_Test_Hybrid/App.ico`
- `Final_Test_Hybrid/Final_Test_Hybrid.csproj`
- `Final_Test_Hybrid/Form1.cs`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Form1.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-app-icon-warning.txt" -e=WARNING` — только baseline warning про поле `_config`, новых warning по новой логике иконки нет.
- Проверка ярлыка через `WScript.Shell` подтвердила `IconLocation` и `TargetPath` для `C:\Users\Alexander\Desktop\Final Test.lnk`.

## Residual Risks

- Windows может кэшировать иконки; для визуального обновления может потребоваться `F5` на рабочем столе или повторный запуск приложения.
- Ярлык привязан к `Debug`-сборке в текущем рабочем каталоге. Если пользователь очистит `bin` или переключится на другой output-path, ярлык нужно будет пересоздать или перепривязать.
- В решении остаётся baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`; текущий change-set его не затрагивает.

## Инциденты

- no new incident
