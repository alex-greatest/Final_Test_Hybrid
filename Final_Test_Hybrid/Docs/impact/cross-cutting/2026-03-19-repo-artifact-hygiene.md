# 2026-03-19 repo-artifact-hygiene

## Контур

- Git tree hygiene / ignore rules / локальные build и inspect артефакты

## Что изменено

- Корневой `.gitignore` расширен для локальных артефактов, которые не должны попадать в репозиторий:
  - `.codex-build/`;
  - корневые `inspect*.txt`;
  - `*.stackdump`;
  - `*scratchpadfull-diff.txt`;
  - весь `%TEMP%/`;
  - `/.idea/`;
  - `/.vs/`;
  - `/.playwright-mcp/`;
  - корневые `/_tmp_*`.
- Для Rider добавлено точечное исключение из ignore:
  - разрешён только `.idea/.idea.Final_Test_Hybrid/.idea/vcs.xml`;
  - остальные `.idea`-файлы и локальное IDE-состояние остаются игнорируемыми.
- Причина исключения: Rider периодически терял VCS mapping проекта, потому что `vcs.xml` не восстанавливался из репозитория и локально исчезал, хотя CLI `git` оставался исправным.
- Из индекса удалены уже попавшие артефакты:
  - `.codex-build/obj/*`;
  - корневые `inspect*.txt`;
  - `bash.exe.stackdump`;
  - `CUsersALEXAN~1AppDataLocalTempclaudeD--projects-Final-Test-Hybrid9e99d8f2-2af3-4513-b9b4-5284331b2d7escratchpadfull-diff.txt`;
  - `%TEMP%/inspect-result-storage.txt`;
  - `.idea/.idea.Final_Test_Hybrid/.idea/*`;
  - `.vs/Final_Test_Hybrid.slnx/*`;
  - `.vs/ProjectEvaluation/*`;
  - `.playwright-mcp/screenshot_analysis.png`;
  - `_tmp_readsoft_parse.py`;
  - `_tmp_readsoft_table.md`.
- Из workspace дополнительно удалены локальные inspect-отчёты:
  - корневые `inspect*.txt` и `inspectcode*.txt`;
  - `artifacts/inspect*.txt`;
  - `%TEMP%/inspect*.txt`;
  - `.tmp_build_out/inspect*.txt` и `.tmp_build_out/inspectcode*.txt`.
- `git clean -fX` удалил также пустые/игнорируемые каталоги `%TEMP%/` и `.tmp_build_out/`, потому что внутри оставались только такие локальные артефакты.
- Нерелевантные рабочие изменения пользователя в runtime/UI/diagnostic контуре не затрагивались.
- Процессный пробел зафиксирован отдельно: `AGENTS.md` ссылается на `Final_Test_Hybrid/Docs/impact/ImpactHistoryGuide.md`, но такого файла в repo нет.

## Затронутые файлы

- `.gitignore`
- `.idea/.idea.Final_Test_Hybrid/.idea/vcs.xml`
- `Final_Test_Hybrid/Docs/impact/cross-cutting/2026-03-19-repo-artifact-hygiene.md`

## Проверки

- `git status --short --ignored -- .gitignore .codex-build inspect*.txt "*.stackdump" "*scratchpadfull-diff.txt" "%TEMP%"` — подтверждено:
- `git status --short --ignored -- .gitignore .codex-build inspect*.txt "*.stackdump" "*scratchpadfull-diff.txt" "%TEMP%"` — подтверждено до физического удаления:
  - ранее tracked артефакты переведены в staged delete из индекса;
  - новые и существующие локальные артефакты попадают под ignore (`!!`).
- `git check-ignore -v .codex-build/modbus-stabilization inspect-hint-modbus-stabilization.txt inspect-warning-modbus-stabilization.txt bash.exe.stackdump` — подтверждено, что ignore-правила срабатывают от корневого `.gitignore`.
- `git clean -fX -- inspect*.txt inspectcode*.txt artifacts/inspect*.txt .tmp_build_out/inspect*.txt .tmp_build_out/inspectcode*.txt %TEMP%/inspect*.txt` — локальные inspect-артефакты удалены из workspace.
- `git ls-files ".idea" ".vs" ".playwright-mcp" "_tmp_readsoft_parse.py" "_tmp_readsoft_table.md"` — подтверждено, что IDE/tool-state и временные readsoft-файлы были ошибочно tracked до cleanup.
- `git check-ignore -v ".idea/dummy" ".vs/dummy" ".playwright-mcp/dummy" "_tmp_readsoft_parse.py" "_tmp_probe.txt"` — подтверждено, что новые ignore-правила матчят IDE/tool-state и корневые временные `_tmp_*`.
- `git check-ignore -v .idea\\.idea.Final_Test_Hybrid\\.idea\\vcs.xml` — до правки подтверждено, что Rider `vcs.xml` ошибочно игнорировался.
- `git status --short -- .gitignore .idea/.idea.Final_Test_Hybrid/.idea/vcs.xml` — после правки `vcs.xml` выходит из ignore и виден как repo-tracked candidate для фиксации VCS mapping.
- `dotnet build` / `dotnet format` / `jb inspectcode` не запускались: change-set не меняет runtime-код, stable behavior и source-of-truth guide; правка ограничена git hygiene.

## Инциденты

- no new incident
