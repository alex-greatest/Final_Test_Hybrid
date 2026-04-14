# 2026-04-08 p-gas-hmi-nok-alert

## Контур

- UI / Main screen / parameter panel
- PLC-driven HMI indication for `P-GAS`

## Что изменено

- В `MainScreenTags` добавлен канонический HMI-tag `GasPressureNok` с node id `ns=3;s="DB_HMI"."GasPressureNOK"`.
- Компонент `Gas.razor` теперь подписывается не только на значение `GasPag`, но и на HMI-флаг `GasPressureNok`.
- Для поля `P-GAS` добавлен локальный opt-in CSS-класс `parameter-alert-input`: при активном HMI-флаге textbox получает красный фон, контрастную рамку и тёмный текст.
- Подсветка влияет только на оформление `P-GAS`; остальные поля блока `Gas` и источник самого значения давления не менялись.
- `Docs/ui/MainScreenGuide.md` синхронизирован: новый alert-state `P-GAS` зафиксирован как часть поведения панели параметров главного экрана.

## Затронутые файлы

- `Final_Test_Hybrid/Components/Main/Parameter/Gas.razor`
- `Final_Test_Hybrid/Models/Plc/Tags/MainScreenTags.cs`
- `Final_Test_Hybrid/wwwroot/css/text_indicator.css`
- `Final_Test_Hybrid/Docs/ui/MainScreenGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx`
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes`
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes`
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Main/Parameter/Gas.razor;Final_Test_Hybrid/Models/Plc/Tags/MainScreenTags.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-p-gas-hmi-nok-alert.txt" -e=WARNING`

## Residual Risks

- В этом сеансе не выполнялась интерактивная визуальная проверка desktop UI; читаемость подтверждается кодом и локальным контрастным стилем.
- Если PLC фактически публикует другой path для `DB_HMI.GasPressureNOK`, подсветка не сработает до корректировки node id.

## Инциденты

- `no new incident`
