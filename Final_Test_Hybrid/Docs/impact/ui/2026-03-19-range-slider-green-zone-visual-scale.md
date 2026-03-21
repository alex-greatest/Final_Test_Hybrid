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
- Риски переведены на фиксированный набор опорных точек:
  - `Min`;
  - midpoint левой красной зоны;
  - `GreenStart`;
  - дополнительная внутренняя насечка между `GreenStart` и серединой зелёной зоны;
  - середина зелёной зоны;
  - дополнительная внутренняя насечка между серединой зелёной зоны и `GreenEnd`;
  - `GreenEnd`;
  - midpoint правой красной зоны;
  - `Max`.
- Подписи шкалы больше не зависят от правила `каждая вторая риска` и теперь строятся только из ключевых точек:
  - `Min`;
  - `GreenStart`;
  - середина зелёной зоны;
  - `GreenEnd`;
  - `Max`.
- Для подписей добавлено авто-форматирование точности отображения:
  - исходные значения `Min/Max/GreenZoneStart/GreenZoneEnd` и runtime-логика не меняются;
  - UI повышает число знаков после запятой только если при базовом округлении разные ключевые точки выглядели бы одинаково.
- Для пузыря текущего значения добавлена отдельная авто-точность отображения:
  - положение указателя продолжает использовать точное значение;
  - текст в пузыре использует ту же стабильную точность, что и подписи шкалы, без пересчёта precision на каждом новом значении.
- Для одновременно показанных слайдеров точность отображения синхронизирована:
  - `RangeSliderDisplay` считает требуемую точность для каждого активного слайдера только по ключевым точкам шкалы;
  - всем активным `RangeSlider` передаётся общий максимум, чтобы экран не смешивал `1` и `2` знака после запятой в одном блоке.
- Это убирает кратковременные скачки формата вида `15.0 -> 15.000 -> 15.0`, которые появлялись из-за включения текущего `Value` в расчёт precision.
- Обновление значений и переключение layout разделены по событиям:
  - `RangeSliderUiState.OnStateChanged` остаётся для локального обновления `RangeSliderDisplay`;
  - `RangeSliderUiState.OnVisibilityChanged` добавлен для `MyComponent`, чтобы главный экран реагировал только на показ/скрытие блока слайдеров, а не на каждый тик значения.
- Это убирает лишнюю перерисовку условной ветки `Main Screen`, из-за которой блок `RangeSlider` мог визуально промаргивать во время работы.
- Крайние подписи `Min/Max` выровнены по краям шкалы, чтобы правый край не выглядел пустым или визуально повреждённым.
- Численные `Min/Max/GreenZoneStart/GreenZoneEnd` и логика `RangeSliderUiState` не менялись.
- Добавлен guard для вырожденной шкалы `Min == Max`, чтобы генерация рисок не делила на нулевой диапазон.

## Затронутые файлы

- `Final_Test_Hybrid/RangeSlider.razor`
- `Final_Test_Hybrid/Components/Main/RangeSliderDisplay.razor`
- `Final_Test_Hybrid/MyComponent.razor`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Completion/RangeSliderUiState.cs`
- `Final_Test_Hybrid/Docs/ui/MainScreenGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остался внешний warning `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0`, не связан с этой правкой.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/RangeSlider.razor;Final_Test_Hybrid/Components/Main/RangeSliderDisplay.razor;Final_Test_Hybrid/MyComponent.razor;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Completion/RangeSliderUiState.cs" --no-build --format=Text "--output=inspect-warning-range-slider.txt" -e=WARNING` — без warning по итоговому отчёту.

## Инциденты

- no new incident
