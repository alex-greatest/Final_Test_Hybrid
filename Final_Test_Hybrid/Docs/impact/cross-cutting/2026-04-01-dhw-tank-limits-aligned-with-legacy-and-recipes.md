# 2026-04-01 dhw-tank-limits-aligned-with-legacy-and-recipes

## Контур

- `DHW/Set_Tank_Mode`
- `DHW/Check_Tank_Mode`
- контракт пределов runtime-результатов `Tank_DHW_Mode` и `Tank_DHW_Press`

## Что изменено

- В `SetTankModeStep` исправлен контракт пределов для `Tank_DHW_Mode`.
- Шаг больше не использует `DB_Recipe.DHW.Tank.Mode = 2.5 bar` как нижний предел.
- Для `Tank_DHW_Mode` теперь используется:
  - `min = DB_Recipe.DHW.Tank.WaterMin`
  - `max = DB_Recipe.DHW.Tank.WaterMax`
- `GetLimits(...)` для `DHW/Set_Tank_Mode` теперь показывает диапазон `WaterMin .. WaterMax`.
- В `CheckTankModeStep` исправлен контракт пределов для `Tank_DHW_Press`.
- Шаг больше не использует `DB_Recipe.DHW.Tank.WaterMin/WaterMax = 5..20 L/min` как pressure-пределы.
- Значение `Tank_DHW_Press` теперь читается из `DB_Parameter.DHW.Tank_Mode`.
- Для `Tank_DHW_Press` теперь используется:
  - `min = DB_Recipe.DHW.Tank.Mode - DB_Recipe.DHW.PresTest.Tol`
  - `max = DB_Recipe.DHW.Tank.Mode + DB_Recipe.DHW.PresTest.Tol`
- PLC runtime-логика обоих шагов не менялась:
  - `End => Pass`
  - `Error => Fail`
  - отдельная локальная валидация фактических значений по диапазону не вводилась.
- В `Docs/execution/StepsGuide.md` добавлены stable-doc фиксации для обоих шагов.
- Добавлены regression-тесты на `GetLimits(...)`, которые подтверждают:
  - `Tank_DHW_Mode -> [5,0 .. 20,0]`
  - `Tank_DHW_Press -> Tank.Mode ± Tol` без hardcode допуска

## Источники решения

- Recipe-таблица `traceability_new`:
  - `DHW.Tank.WaterMin = 5.0 L/min`
  - `DHW.Tank.WaterMax = 20.0 L/min`
  - `DHW.Tank.Mode = 2.5 bar`
  - `DHW.PresTest.Value = 2.5 bar`
  - `DHW.PresTest.Tol = 0.3 bar`
- Контракт `Tank_DHW_Mode = WaterMin .. WaterMax` уточнён после дополнительной сверки recipe-таблицы и подтверждения, что для этого параметра пределы должны браться именно из `WaterMin/WaterMax`
- Контракт `Tank_DHW_Press` уточнён отдельно: значение берётся из `DB_Parameter.DHW.Tank_Mode`, а центр диапазона из `DB_Recipe.DHW.Tank.Mode`; `DB_Recipe.DHW.PresTest.Value` больше не является source-of-truth для этого шага

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Steps/DHW/SetTankModeStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/DHW/CheckTankModeStep.cs`
- `Final_Test_Hybrid.Tests/Runtime/TankModeStepLimitsTests.cs`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — не завершён: запущенный `Final_Test_Hybrid.exe` блокирует `bin\Debug\net10.0-windows\Final_Test_Hybrid.exe`; до ошибки остаются baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet test Final_Test_Hybrid.Tests\Final_Test_Hybrid.Tests.csproj --filter FullyQualifiedName~TankModeStepLimitsTests` — не завершён по той же причине: активный `Final_Test_Hybrid.exe` блокирует выходные артефакты основного проекта.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/DHW/SetTankModeStep.cs;Final_Test_Hybrid/Services/Steps/Steps/DHW/CheckTankModeStep.cs;Final_Test_Hybrid.Tests/Runtime/TankModeStepLimitsTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-dhw-tank-limits.txt" -e=WARNING` — чисто.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Steps/DHW/SetTankModeStep.cs;Final_Test_Hybrid/Services/Steps/Steps/DHW/CheckTankModeStep.cs;Final_Test_Hybrid.Tests/Runtime/TankModeStepLimitsTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-dhw-tank-limits.txt" -e=HINT` — чисто.

## Residual Risks

- `DB_Recipe.DHW.PresTest.Value = 2.5 bar` остаётся в системе, но больше не является source-of-truth для `DHW/Check_Tank_Mode`; если у этого recipe есть отдельное бизнес-назначение в другом контуре, оно вне данного change-set.
- Полноценные `build/test` в этом сеансе не подтверждены из-за работающего экземпляра приложения, который держит выходные артефакты `Final_Test_Hybrid`.

## Инциденты

- `no new incident`
