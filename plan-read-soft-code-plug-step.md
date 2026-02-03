# План: ReadSoftCodePlugStep — таблица проверок, регистр 1054, Reset-кнопка

## Summary

- Рефакторинг `ReadSoftCodePlugStep*` в “таблицу действий” + один универсальный исполнитель (добавление/изменение проверки = правка одной записи).
- Новая проверка: чтение регистра `1054` (с учётом `_settings.BaseAddressOffset`) и сравнение с рецептом `NumberOfContours` (значения `1/2/3`, без маппинга). При несовпадении — `Fail` с заданным текстом из 3 пунктов.
- Для всех ошибок шага `coms-read-soft-code-plug` выставить `ActivatesResetButton: true`, чтобы активировалась кнопка “Сброс ошибки”.
- 2 страховки: ранняя валидация таблицы действий и централизованные билдеры сообщений/логов.

## Scope / Constraints

- Меняем только `ReadSoftCodePlugStep*` и `ErrorDefinitions` (ошибки шага), без изменений других шагов/DI.
- Safety-critical: сохранить поведение существующих проверок и формулировки логов/ошибок максимально 1:1 (кроме добавляемой новой проверки).

## Ошибки: `ActivatesResetButton: true` для всех ошибок шага

### Как включается кнопка Reset

- Ошибка из `TestStepResult.Fail(..., errors: [...])` поднимается в `ErrorService` через `ColumnExecutor`.
- Кнопка “Сброс ошибки” активна, если `ErrorService.HasResettableErrors == true`.
- `HasResettableErrors` становится `true`, если среди активных ошибок есть хоть одна с `ActiveError.ActivatesResetButton == true`.
- `ActiveError.ActivatesResetButton` копируется из `ErrorDefinition.ActivatesResetButton`.

### Что меняем

В `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs`:

1) Для всех `ErrorDefinition` шага `coms-read-soft-code-plug` добавить `ActivatesResetButton: true`:

- `EcuArticleMismatch`
- `EcuBoilerTypeMismatch`
- `EcuPumpTypeMismatch`
- `EcuPressureDeviceTypeMismatch`
- `EcuGasRegulatorTypeMismatch`
- `EcuMaxChHeatOutputMismatch`
- `EcuMaxDhwHeatOutputMismatch`
- `EcuMinChHeatOutputMismatch`
- `EcuPumpModeMismatch`
- `EcuPumpPowerMismatch`
- `EcuGasTypeMismatch`
- `EcuCurrentOffsetMismatch`
- `EcuFlowCoefficientMismatch`
- `EcuMaxPumpAutoPowerMismatch`
- `EcuMinPumpAutoPowerMismatch`
- `EcuComfortHysteresisMismatch`
- `EcuMaxFlowTemperatureMismatch`
- `ThermostatJumperMissing`

2) Добавить новую ошибку для проверки 1054:

- имя: `EcuConnectionTypeMismatch`
- код: `П-016-26` (следующий свободный после `П-016-25`)
- описание: `Несовпадение типа подключения к котлу (1054)`
- `Severity: ErrorSeverity.Critical`
- `ActivatesResetButton: true`
- `RelatedStepId: "coms-read-soft-code-plug"`
- `RelatedStepName: "Coms/Read_Soft_Code_Plug"`

## Новая проверка: регистр 1054

### Дока

- `Final_Test_Hybrid/Диагностический_протокол_1_8_10.md`: регистр `1054` — “тип подключения к котлу”, значения `0..3`.

### Точная реализация

- Константа: `private const ushort RegisterConnectionType = 1054;`
- Выполнять проверку первой (после `DelayAsync(...)`):

1) `var expected = context.RecipeProvider.GetValue<ushort>("NumberOfContours")!.Value;`
2) `var address = (ushort)(RegisterConnectionType - _settings.BaseAddressOffset);`
3) `var read = await context.DiagReader.ReadUInt16Async(address, ct);`
4) Если `!read.Success` → `return TestStepResult.Fail(<сообщение об ошибке чтения>)` (как принято в шаге для read-fail).
5) `var actual = read.Value;`
6) Если `actual != expected` → вернуть:

```
TestStepResult.Fail(
    "1. Отсканированный код на котле не соответствует котлу;\n" +
    "2. На котёл установлен не правильный жгут;\n" +
    "3. Жгут повреждён.",
    errors: [ErrorDefinitions.EcuConnectionTypeMismatch]);
```

7) Если равно → продолжаем выполнение следующих действий.

- В `testResultsService` этот пункт не писать.

## Рефакторинг `ReadSoftCodePlugStep*` в “таблицу действий”

### Цель

- Убрать копипасту из ~20 методов `ReadAndVerify*` и длинный `ExecuteAsync`.
- Сделать изменения “табличными”: добавление/удаление/правка параметра = правка одной записи.
- Держать каждый файл < 300 строк.

### Целевое разбиение файлов

1) `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.cs`

- константы регистров/recipe keys/result names
- `Id/Name/Description`
- `ExecuteAsync` (delay + цикл по actions)
- `RequiredRecipeAddresses` (строится из таблицы)
- `ClearPreviousResults` (строится из таблицы)
- `IsDualCircuit` (как сейчас)

2) Новый файл: `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.cs`

- модели действий (records)
- `BuildActions()`
- `ValidateActions()` (страховка №1)
- централизованные билдеры сообщений/логов (страховка №2)
- `ExecuteActionAsync` (switch по типу действия) + маленькие исполнители

3) `ReadSoftCodePlugStep.Verification.cs` и `ReadSoftCodePlugStep.ReadOnly.cs`

- удалить или сократить до минимума (логика переносится в `Actions.cs`).

### Модели действий (records)

- База: `abstract record SoftCodePlugAction(int StepNo, string Title);`
- Типы:
  - `VerifyUInt16Action` (читать `UInt16`, сравнить с recipe expected, записать results, mismatch → error)
  - `VerifyStringAction` (строка, сравнить, записать results, mismatch → error)
  - `ReadOnlyStringAction` (строка, без верификации, записать results)
  - `ReadOnlyUInt32Action` (`UInt32`, без верификации, записать results)
  - `ThermostatJumperCheckAction` (как сейчас: если 0 → fail + `ThermostatJumperMissing`, без results)
  - `VerifyConnectionType1054Action` (новая проверка 1054, compare с recipe, fail-текст 3 пункта, error `EcuConnectionTypeMismatch`, без results)

Каждый action содержит только данные: регистр/диапазон/длина, recipe keys, result name/unit, mismatch error, `ShouldRun` + `SkipLogMessage` (для dual-circuit).

### Таблица действий: `BuildActions()`

- Возвращает список действий в порядке выполнения.
- Содержит новый пункт 1054 первым.
- Дальше — существующие проверки в текущем порядке, включая:
  - dual-circuit-only: FlowCoefficient и ComfortHysteresis (через `ShouldRun/SkipLogMessage`),
  - read-only пункты 18–21,
  - jumper check (22).

## Страховки

### 1) `ValidateActions()` (ранняя валидация)

До любых чтений:

- `StepNo` уникальны и непрерывны (1..N).
- Для verify-action: нужные recipe keys не пустые.
- Для result-writing action: `ResultName` не пустой.
- Если задан `ShouldRun`, то обязателен `SkipLogMessage`.
- `maxLength > 0` для строк; диапазоны корректны.

При нарушении: `LogError(...)` и `TestStepResult.Fail("Ошибка конфигурации шага ...")`.

### 2) Централизация сообщений/логов

- Единые методы, которые формируют строки ошибок чтения, mismatch и OK/NOK логи.
- Цель: минимизировать расхождение формулировок при будущих правках и удержать 1:1 тексты.

## Тестирование и приемка

### Локально

- `dotnet build`

### На стенде

1) Несовпадение 1054 → `Fail` с 3 пунктами + активная ошибка `П-016-26` + кнопка Reset активна.
2) Несовпадение любого параметра шага → соответствующая ошибка активна + кнопка Reset активна (из-за `ActivatesResetButton: true`).
3) Single/Dual circuit: пропуски dual-only параметров работают как раньше.
4) Ошибка чтения регистра: шаг падает как раньше (сообщение/лог), без неожиданных side effects.

## Assumptions

- Рецепт `NumberOfContours` содержит ожидаемое значение `1/2/3` (без маппинга).
- Включение Reset-кнопки для всех ошибок шага `coms-read-soft-code-plug` ожидаемо и согласовано (вы запросили явно).

