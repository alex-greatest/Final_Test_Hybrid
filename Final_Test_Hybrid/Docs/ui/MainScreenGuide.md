# Руководство по главному экрану

## Цель

Описать структуру и ключевые UI-переходы вкладки «Главный экран», чтобы изменения в экране были предсказуемыми и не ломали операторский сценарий.

## Источник

- `Final_Test_Hybrid/MyComponent.razor`
- `Final_Test_Hybrid/MyComponent.razor.css`
- `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor`
- `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor.css`

## Позиция в приложении

Главный экран — первая верхнеуровневая вкладка в `MyComponent.razor` (`RadzenTabsItem Text="Главный экран"`).

## Композиция экрана

### 1) Верхняя карточка (header-card)

Сетка `header-grid` делится на 3 блока:

1. Левый блок: `Shift`, `BoilerOrder`.
2. Средний блок: `BoilerInfo`.
3. Правый блок: `OperatorInfo`.

Назначение: ввод/отображение операционных данных до и во время прогона.

### 2) Панель текущих параметров

В `header-grid-parameter` размещены:

- `Gas`
- `Delta`
- `DHW`
- `CH`
- `Emissions`
- `GasP`

Все компоненты подписываются с `EmitCachedValueImmediately="true"`.

### 3) Основная рабочая зона (условный рендер)

Порядок приоритета контента:

1. `TestCompletionUiState.ShowResultImage == true`  
   Показывается итоговое изображение результата с подсказкой по кнопкам завершения/повтора.
2. `RangeSliderUiState.HasActiveSliders == true`  
   Показывается `RangeSliderDisplay`.
3. Иначе  
   Показываются `MessageHelper` + `TestSequenseGrid`.

## Грид последовательности шага

`TestSequenseGrid` использует `class="main-grid-legacy"` и остаётся отдельным историческим профилем.

Причины:

1. Компактная плотность строк и статусы по цвету (`error`, `success`, `running`).
2. Поведение и визуал завязаны на операторский сценарий главного экрана.
3. Массовый перевод в `grid-unified` без отдельного инцидента запрещён.

## Нижняя статусная зона (`app-bottom-10`)

Композиция из трёх участков:

1. Слева: `ErrorResetButton`, затем индикаторы `AirOnIndicator`, `EStopIndicator`, `GasStopIndicator`.
2. Центр: `TestTimerDisplay`, `ChangeoverTimerDisplay`, `BoilerStatusDisplay`.
3. Справа: `DatabaseIndicator`, `PlcIndicator`, `SpringBootIndicator`, `ScannerIndicator`.

## События и обновление UI

`MyComponent` подписывается на состояния:

- `PlcSubscriptionState.OnStateChanged`
- `OpcUaConnectionState.ConnectionStateChanged`
- `TestCompletionUiState.OnStateChanged`
- `RangeSliderUiState.OnStateChanged`

Дополнительно показ оверлея подписки:

- `SubscriptionLoadingOverlay IsVisible="@SubscriptionState.IsInitializing"`

## Границы ответственности

1. Главный экран отвечает за композицию и отображение состояния.
2. Runtime-решения (reset, interrupt, pipeline gating) остаются в сервисах/координаторах.
3. UI не должен подменять runtime-контроль критичных действий.

## Do

- Поддерживать стабильную геометрию зон header/content/footer.
- Проверять все три режима контента (result image, slider, sequence grid).
- Сохранять `main-grid-legacy` только для `TestSequenseGrid`.

## Don't

- Не менять приоритет условного рендера без отдельного согласования.
- Не переносить критичные runtime-решения в визуальные флаги компонента.
- Не убирать нижние индикаторы из фиксированной структуры без замены на эквивалент.

## Чек-лист ревью главного экрана

1. Header-сетка остаётся трёхблочной и читаемой.
2. Основная зона корректно работает в трёх режимах отображения.
3. `TestSequenseGrid` сохраняет профиль `main-grid-legacy`.
4. Нижняя панель остаётся в схеме left/center/right.
5. Подписки на state-сервисы корректно отписываются в `Dispose`.
