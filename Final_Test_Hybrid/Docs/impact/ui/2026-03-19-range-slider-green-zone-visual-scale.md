# 2026-03-19 range-slider-green-zone-visual-scale

## Контур

- UI / Main screen / RangeSlider

## Что изменено

- В `RangeSlider.razor` введён piecewise-визуальный remap шкалы без изменения runtime-контрактов.
- Визуальные доли полосы зафиксированы как `15% / 70% / 15%`:
  - левая красная зона занимает `15%`;
  - зелёная зона занимает `70%`;
  - правая красная зона занимает `15%`.
- На новый remap переведены:
  - положение указателя значения;
  - риски;
  - подписи шкалы.
- Плотность рисок теперь разделена по сегментам:
  - в красных зонах остаётся не более одной промежуточной риски на сторону;
  - в зелёной зоне добавляются дополнительные промежуточные риски без визуального перегруза.
- Подписи шкалы ограничены и теперь выводятся выборочно:
  - ключевые точки (`Min`, `GreenStart`, середина зелёной зоны, `GreenEnd`, `Max`) остаются всегда;
  - внутри зелёной зоны дополнительно подписывается только каждая вторая промежуточная риска.
- Численные `Min/Max/GreenZoneStart/GreenZoneEnd` и логика `RangeSliderUiState` не менялись.
- Добавлен guard для вырожденной шкалы `Min == Max`, чтобы генерация рисок не делила на нулевой диапазон.

## Затронутые файлы

- `Final_Test_Hybrid/RangeSlider.razor`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остался внешний warning `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0`, не связан с этой правкой.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/RangeSlider.razor" --no-build --format=Text "--output=inspect-warning-range-slider.txt" -e=WARNING` — без warning по отчёту.

## Инциденты

- no new incident
