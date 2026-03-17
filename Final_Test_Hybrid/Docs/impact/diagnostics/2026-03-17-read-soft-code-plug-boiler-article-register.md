# Impact: Выравнивание Read_Soft_Code_Plug с регистром артикула котла 1175..1181

## Контекст

- Контур: `diagnostics`
- Затронутые подсистемы: `Modbus diagnostic steps`, `traceability impact history`
- Тип изменения: `новый impact`
- Статус цепочки: `завершено`

## Почему делали

- Проблема / цель: шаг `Coms/Read_Soft_Code_Plug` проверял артикул по диапазону `1139..1145`, хотя `Coms/Write_Soft_Code_Plug` записывает артикул котла в `1175..1181`.
- Причина сейчас: после записи и последующей проверки шаг сравнивал `boilerState.Article` не с тем диапазоном регистров, что давало ложную рассинхронизацию между write/read контрактом внутри одного diagnostic flow.

## Что изменили

- Перевели чтение артикула в `ReadSoftCodePlugStep` на регистр `1175` с диапазоном `1175..1181`.
- Выровняли внутреннее имя константы и логи шага под семантику артикула котла.
- Сохранили текущий runtime-контракт шага без расширения scope:
  `Nomenclature_EngP3`, `Soft_Code_Plug`, `EcuArticleMismatch` и остальная таблица действий не менялись.
- Отдельный diagnostic helper `BoilerDeviceInfoService` не трогали: он по-прежнему различает `1139..1145` как артикул изделия и `1175..1181` как артикул котла.

## Где изменили

- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.cs` — заменили стартовый регистр артикула на `1175`.
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Table.Part1.cs` — выровняли action проверки артикула и связанные log/error message под `1175..1181`.

## Когда делали

- Исследование: `2026-03-17 15:57 +04:00`
- Решение: `2026-03-17 16:00 +04:00`
- Правки: `2026-03-17 16:01 +04:00` - `2026-03-17 16:02 +04:00`
- Проверки: `2026-03-17 16:03 +04:00` - `2026-03-17 16:06 +04:00`
- Финализация: `2026-03-17 16:07 +04:00`

## Хронология

| Дата и время | Что сделали | Зачем |
|---|---|---|
| `2026-03-17 15:57 +04:00` | Сверили `ReadSoftCodePlugStep`, `WriteSoftCodePlugStep`, диагностический протокол и `BoilerDeviceInfoService`. | Подтвердить, что ошибка локальна именно в шаге проверки артикула, а не в общем diagnostic API. |
| `2026-03-17 16:00 +04:00` | Зафиксировали scope только на `ReadSoftCodePlugStep*`. | Не смешивать исправление шага с отдельным контрактом `BoilerDeviceInfoService`. |
| `2026-03-17 16:01 +04:00` | Перевели action проверки артикула на диапазон `1175..1181` и выровняли лог-сообщения. | Сделать read/write контракт артикула внутри Soft Code Plug согласованным. |
| `2026-03-17 16:03 +04:00` | Проверили `ReadSoftCodePlugStep*` через статический grep и Rider file problems. | Убедиться, что в шаге не осталось старого адреса и что правка не внесла локальных ошибок. |
| `2026-03-17 16:03 +04:00` - `2026-03-17 16:06 +04:00` | Прогнали `dotnet build`, оба `dotnet format --verify-no-changes` и точечный `jb inspectcode`. | Закрыть обязательные quality gates по AGENTS для code change. |

## Проверки

- Команды / проверки:
  - `dotnet build Final_Test_Hybrid.slnx`
  - `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes`
  - `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes`
  - Rider `get_file_problems` для:
    - `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.cs`
    - `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Table.Part1.cs`
  - `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.cs;Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Table.Part1.cs" --no-build --format=Text "--output=inspectcode-read-soft-code-plug-step.txt" -e=WARNING`
- Результат:
  - `dotnet build` успешен;
  - сохранён существующий warning `MSB3277` по конфликту `WindowsBase`, не связанный с текущей правкой;
  - оба `dotnet format --verify-no-changes` прошли;
  - Rider `get_file_problems` по изменённым файлам ошибок и предупреждений не показал;
  - точечный `inspectcode` завершился без findings в отчёте.

## Риски

- Контракт шага теперь осознанно расходится с `ReadArticleNumberAsync()` из `BoilerDeviceInfoService`: шаг проверяет именно артикул котла, а helper продолжает предоставлять оба канала чтения идентификации. Это нормально, пока callers понимают разницу.
- Downstream имя результата `Nomenclature_EngP3` исторически осталось прежним, хотя теперь значение подтверждается из диапазона артикула котла. Переименование не делалось, чтобы не ломать существующие контракты.

## Открытые хвосты

- Stable docs не менялись: в `Final_Test_Hybrid/Docs/*` не найдено source-of-truth описания этого шага с жёсткой привязкой к `1139..1145`.
- Отдельная ревизия `BoilerDeviceInfoService` на фактическое использование в приложении остаётся вне scope этой задачи.
- `no new incident`

## Связанные планы и документы

- План: `plan-read-soft-code-plug-step.md` + пользовательский implementation plan от `2026-03-17` по выравниванию `ReadSoftCodePlugStep` с записью в `1175..1181`.
- Stable docs:
  - `AGENTS.md`
  - `Final_Test_Hybrid/Docs/execution/StepsGuide.md`
  - `Final_Test_Hybrid/Docs/diagnostics/DiagnosticGuide.md`
  - `Final_Test_Hybrid/Docs/impact/ImpactHistoryGuide.md`
- Related impact:
  - `Active impact по contour diagnostics отсутствовал`
  - `Final_Test_Hybrid/Docs/impact/cross-cutting/2026-03-17-sequence-clear-mode-doc-sync.md` был прочитан как последний active cross-cutting impact рабочей ветки

## Сводит impact

- `Не применимо`
