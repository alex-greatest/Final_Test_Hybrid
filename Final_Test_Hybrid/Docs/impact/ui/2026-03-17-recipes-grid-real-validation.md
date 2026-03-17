# Impact: REAL-валидация в RecipesGrid

## Контекст

- Контур: `ui`
- Затронутые подсистемы: `StandDatabase`, `RecipesGrid`, локальная валидация значения рецепта
- Тип изменения: `новый impact`
- Статус цепочки: `завершено`

## Почему делали

- Проблема / цель: `RecipesGrid` отклонял корректные значения для `PlcType.REAL`, если число вводили с точкой, хотя runtime уже принимает и `,`, и `.`.
- Причина сейчас: локальная UI-валидация использовала culture-sensitive `float.TryParse(item.Value, out _)`, из-за чего поведение зависело от `CurrentCulture` потока и расходилось с runtime-контрактом.

## Что изменили

- Для `PlcType.REAL` заменили локаль-зависимую проверку на helper с нормализацией `,` в `.` и парсингом через `CultureInfo.InvariantCulture`.
- Разрешили в UI-валидации три безопасных сценария ввода `REAL`: целое число, дробное с `,`, дробное с `.`.
- Формат введённой строки не нормализовали и не переписывали: в модель и БД уходит ровно то значение, которое ввёл оператор.
- `INT16`, `DINT`, `BOOL`, `STRING`, сохранение, runtime-парсинг и pipeline не меняли.
- `no new incident`

## Где изменили

- `Final_Test_Hybrid/Components/Engineer/StandDatabase/Recipe/RecipesGrid.razor.cs` — добавили helper `IsValidRealValue(...)` и перевели `PlcType.REAL` на invariant-парсинг.

## Когда делали

- Исследование: `2026-03-17 14:12 +04:00`
- Решение: `2026-03-17 14:27 +04:00`
- Правки: `2026-03-17 14:31 +04:00`
- Проверки: `2026-03-17 14:36 +04:00`
- Финализация: `2026-03-17 14:37 +04:00`

## Хронология

| Дата и время | Что сделали | Зачем |
|---|---|---|
| `2026-03-17 14:12 +04:00` | Прочитали `Docs/ui/README.md`, `Docs/ui/UiPrinciplesGuide.md`, `Docs/impact/ImpactHistoryGuide.md` и проверили active impact по UI. | Подтвердить правила change-set и отсутствие релевантной UI-history по этому контуру. |
| `2026-03-17 14:18 +04:00` | Сверили `RecipesGrid` с runtime-парсингом в `RecipeProvider` и `ScanStepBase`. | Проверить, что проблема локальна для UI и не требует изменения runtime-контракта. |
| `2026-03-17 14:31 +04:00` | В `RecipesGrid.razor.cs` заменили `float.TryParse(item.Value, out _)` на helper с `InvariantCulture` и нормализацией разделителя. | Синхронизировать UI-валидацию `REAL` с существующим runtime-поведением без смены формата хранения. |
| `2026-03-17 14:36 +04:00` | Выполнили `dotnet build`, `dotnet format` verify, `Rider get_file_problems`, точечный `inspectcode` и governance replay audit. | Подтвердить, что локальная правка не внесла ошибок и соответствует AGENTS workflow. |

## Проверки

- Команды / проверки: `mcp__jetbrains_rider__get_file_problems Final_Test_Hybrid/Components/Engineer/StandDatabase/Recipe/RecipesGrid.razor.cs`
- Результат: `ошибок и предупреждений по изменённому файлу не найдено`
- Команды / проверки: `dotnet build Final_Test_Hybrid.slnx`
- Результат: `успешно; остался существующий warning MSB3277 по конфликту WindowsBase, не связан с этим change-set`
- Команды / проверки: `dotnet format analyzers --verify-no-changes`
- Результат: `успешно`
- Команды / проверки: `dotnet format style --verify-no-changes`
- Результат: `успешно`
- Команды / проверки: `jb inspectcode Final_Test_Hybrid.slnx "--include=**/RecipesGrid.razor.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\inspectcode-recipes-grid.txt" -e=WARNING`
- Результат: `замечаний по изменённому файлу нет`
- Команды / проверки: `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\agents-operational-guardrails\scripts\replay_governance_audit.ps1 -RepoRoot . -RequireDocsOnCodeChange`
- Результат: `required headers OK, impact-note guidance OK`

## Риски

- UI теперь пропускает оба дробных разделителя для `REAL`; это намеренно и соответствует уже существующему runtime-парсингу.
- Change-set не нормализует формат строки перед сохранением, поэтому в БД по-прежнему могут встречаться оба варианта записи дроби. Для текущего runtime это допустимо.

## Открытые хвосты

- Не применимо.

## Связанные планы и документы

- План: `локальная правка REAL-валидации в RecipesGrid`
- Stable docs: `Final_Test_Hybrid/Docs/ui/README.md`, `Final_Test_Hybrid/Docs/ui/UiPrinciplesGuide.md`, `Final_Test_Hybrid/Docs/impact/ImpactHistoryGuide.md`
- Related impact: `в active UI impact релевантной истории не было; cross-cutting impact по этой теме отсутствует`

## Сводит impact

- Не применимо.
