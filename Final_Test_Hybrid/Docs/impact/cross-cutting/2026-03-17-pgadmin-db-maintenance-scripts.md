# Impact: Чистые SQL-скрипты для pgAdmin по рецептам и ошибкам

## Контекст

- Контур: `cross-cutting`
- Затронутые подсистемы: `db maintenance`, `traceability_boiler`, `ops workflow`
- Тип изменения: `новый impact`
- Статус цепочки: `завершено`

## Почему делали

- Проблема / цель: исходные материалы для обновления `tb_recipe` и пересева `tb_error_settings_*` были распределены между markdown-документом и SQL-скриптом с поясняющими комментариями, из-за чего их было неудобно целиком копировать в `pgAdmin`.
- Причина сейчас: оператору понадобились отдельные чистые SQL-файлы без markdown-обвязки и без комментариев, пригодные для прямого copy-paste в окно запросов `pgAdmin`.

## Что изменили

- Добавили отдельный SQL-файл с обновлением рецептов для `traceability_boiler`, собранный из `update_recipe_final_test.md` и обёрнутый в транзакцию.
- Добавили отдельный SQL-файл с reseed ошибок для `traceability_boiler`, эквивалентный существующему maintenance-скрипту, но без комментариев.
- Исходные файлы с пояснениями и описанием не меняли, чтобы сохранить их как человекочитаемый источник.

## Где изменили

- `Final_Test_Hybrid/tools/db-maintenance/update_traceability_boiler_recipes_for_pgadmin.sql` — добавили чистый SQL для обновления `tb_recipe`.
- `Final_Test_Hybrid/tools/db-maintenance/reseed_traceability_boiler_errors_from_program_for_pgadmin.sql` — добавили чистый SQL для reseed `tb_error_settings_template` и `tb_error_settings_history`.

## Когда делали

- Исследование: `2026-03-17 11:36 +04:00`
- Решение: `2026-03-17 11:38 +04:00`
- Правки: `2026-03-17 11:39 +04:00`
- Проверки: `2026-03-17 11:41 +04:00`
- Финализация: `2026-03-17 11:42 +04:00`

## Хронология

| Дата и время | Что сделали | Зачем |
|---|---|---|
| `2026-03-17 11:36 +04:00` | Проверили `AGENTS.md`, `ImpactHistoryGuide.md`, `tools/db-maintenance/README.md` и наличие релевантной impact-истории. | Подтвердить рабочий контур и не пропустить обязательный governance-порядок. |
| `2026-03-17 11:38 +04:00` | Прочитали исходные материалы `update_recipe_final_test.md` и `reseed_traceability_boiler_errors_from_program.sql`. | Собрать точный исполняемый SQL без ручных догадок по содержимому. |
| `2026-03-17 11:39 +04:00` | Добавили два новых `.sql`-файла для `pgAdmin`, не меняя исходные документы с пояснениями. | Дать оператору copy-paste friendly артефакты и сохранить исходные человекочитаемые источники. |
| `2026-03-17 11:41 +04:00` | Проверили новые файлы на отсутствие комментариев и markdown-маркеров. | Убедиться, что файлы действительно чистые и пригодны для прямого запуска. |

## Проверки

- Команды / проверки:
  - `Select-String -Path 'D:\projects\Final_Test_Hybrid\Final_Test_Hybrid\tools\db-maintenance\update_traceability_boiler_recipes_for_pgadmin.sql','D:\projects\Final_Test_Hybrid\Final_Test_Hybrid\tools\db-maintenance\reseed_traceability_boiler_errors_from_program_for_pgadmin.sql' -Pattern '^\s*--','^#','```'`
- Результат:
  - комментарии SQL и markdown-маркеры в новых файлах не найдены;
  - содержимое файлов осталось чистым исполняемым SQL для `pgAdmin`.

## Риски

- Скрипты остаются целевыми для `traceability_boiler`; запуск в другой БД приведёт к изменению не того контура.
- Чистые файлы не содержат встроенных пояснений, поэтому оператор должен брать их по имени и назначению, а не редактировать вручную на лету.

## Открытые хвосты

- Stable docs не обновлялись: поведение приложения и канонический runtime-контур не менялись.
- `no new incident`

## Связанные планы и документы

- План: `Пользовательская задача от 2026-03-17: подготовить два отдельных SQL-файла без комментариев для copy-paste в pgAdmin.`
- Stable docs:
  - `AGENTS.md`
  - `Final_Test_Hybrid/Docs/impact/ImpactHistoryGuide.md`
  - `Final_Test_Hybrid/tools/db-maintenance/README.md`
- Related impact:
  - `Final_Test_Hybrid/Docs/impact/cross-cutting/2026-03-16-impact-history-workflow.md`
  - `DB-maintenance-specific impact ранее отсутствовал.`

## Сводит impact

- `Не применимо`
