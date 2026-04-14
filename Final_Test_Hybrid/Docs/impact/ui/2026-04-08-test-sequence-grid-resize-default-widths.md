# 2026-04-08 test-sequence-grid-resize-default-widths

## Контур

- UI / Main screen / `TestSequenseGrid`
- профиль `main-grid-legacy`

## Что изменено

- В `TestSequenseGrid.razor` включён `AllowColumnResize`, чтобы оператор мог локально подстраивать ширину колонок на главном экране.
- Общий `ColumnWidth` удалён, чтобы стартовая геометрия задавалась только явными `Width`/`MinWidth` колонок и не давала первый перерасчёт ширины при первом drag-resize.
- Для колонок главного грида заданы явные дефолтные ширины и минимальные границы сжатия.
- Колонка `Статус` настроена на `60px` по умолчанию с `MinWidth=40px`, колонка `Результаты` расширена по умолчанию до `230px`, а колонка `Пределы` оставлена `80px`, чтобы основной runtime-текст был виден лучше без изменения общего профиля `main-grid-legacy`.
- После анализа `Radzen` demo/forum и JS-логики `startColumnResize` подтверждено, что скачок вызван несовпадением между стартовой вычисленной шириной полноширинного HTML table и первым inline-width, который `Radzen` начинает писать только во время drag.
- CSS workaround с hidden-scroll baseline снят: он убирал скачок частично, но ломал обратное сжатие колонки и не подходит для `main-grid-legacy`.
- В `wwwroot/js/grid-helpers.js` добавлен локальный helper `mainTestSequenceGridResizeFix.sync`, который после render считывает фактические ширины header cells и синхронно записывает их в `th` и `colgroup col` как inline-width.
- В `TestSequenseGrid.razor` helper регистрируется и вызывается после каждого render и после `Reload()`, чтобы `Radzen` начинал resize из уже зафиксированного baseline и не делал первый скачок. Повторная регистрация безопасна: после успешного patch helper уходит в no-op.
- В тот же helper добавлен локальный wrapper над `Radzen.startColumnResize`: прямо перед стартом resize активной колонке вычисляется `maxWidth` по видимой ширине `.rz-data-grid-data`, чтобы колонка не могла уехать за правую границу hidden-scroll legacy-grid и не делала resizer недоступным. Предыдущий `pointerdown`-hook снят как недостаточно надёжный: он не гарантировал попадание в тот же элемент/момент, который использует Radzen.
- Wrapper сделан fail-safe: если bounds guard не может посчитать ограничения, оригинальный `Radzen.startColumnResize` всё равно вызывается. `maxWidth` выставляется только когда видимый контейнер реально даёт запас на расширение больше текущей ширины, чтобы guard не блокировал сам resize.
- В `TestSequenseGrid.razor.css` сохранено только явное растяжение host/grida (`width:100%`, `min-width:0`, для `main-grid-legacy` также `flex:1`) без смены table-layout/overflow режима.
- В `Docs/ui/GridProfilesGuide.md` и `Docs/ui/MainScreenGuide.md` зафиксировано, что `main-grid-legacy` сохраняет компактную геометрию, допускает ручной resize колонок, стабилизирует первый drag через локальную JS-синхронизацию widths и ограничивает расширение видимой шириной грида.

## Затронутые файлы

- `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor`
- `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor.css`
- `Final_Test_Hybrid/wwwroot/js/grid-helpers.js`
- `Final_Test_Hybrid/Docs/ui/GridProfilesGuide.md`
- `Final_Test_Hybrid/Docs/ui/MainScreenGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остались baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- После промежуточной подстройки ширин (`Статус=40px`, `Результаты=240px`) повторный `dotnet build Final_Test_Hybrid.slnx` упёрся в lock активного `Final_Test_Hybrid.exe` (`MSB3027/MSB3021`); это блок внешнего процесса, а не ошибка компиляции change-set.
- После замены bounds-hook на wrapper `Radzen.startColumnResize` повторный `dotnet build Final_Test_Hybrid.slnx` — успешно; остались только baseline warning `MSB3277`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor;Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor.css" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-test-sequence-grid-resize.txt" -e=WARNING` — отчёт сформирован; warning по целевому change-set не выявлены.

## Residual Risks

- Интерактивная проверка drag-resize в desktop UI в этом сеансе не выполнялась; change-set подтверждается кодом и статическими проверками.
- В решении сохраняются baseline warning `MSB3277` по `WindowsBase`; этот UI change-set их не меняет.

## Инциденты

- `no new incident`
