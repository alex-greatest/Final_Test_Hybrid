# 2026-03-19 stand-database-single-validation-toast

## Контур

- UI / Engineer / Stand database / validation notifications

## Что изменено

- В `RecipesGrid` убран ранний warning-toast на `Value.Change`, который дублировал последующую ошибку сохранения при невалидном числовом значении.
- После правки `RecipesGrid` использует тот же паттерн, что и остальные grid внутри `StandDatabaseDialog`: одно уведомление об ошибке только в `SaveRow`, если валидация не пройдена.
- Поведение остальных grid диалога не менялось: по коду они уже не имели второго warning-toast и оставались в single-notification схеме.

## Затронутые файлы

- `Final_Test_Hybrid/Components/Engineer/StandDatabase/Recipe/RecipesGrid.razor`
- `Final_Test_Hybrid/Components/Engineer/StandDatabase/Recipe/RecipesGrid.razor.cs`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx -p:UseAppHost=false` — в стандартный `bin\Debug` упирался в lock запущенного `Final_Test_Hybrid.dll`, поэтому компиляция подтверждена командой `dotnet build Final_Test_Hybrid.slnx -p:UseAppHost=false -p:OutputPath=D:\projects\Final_Test_Hybrid\.codex-build\stand-database-single-toast-build\`; успешно. Остался внешний warning `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0`, не связанный с этой правкой.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Engineer/StandDatabase/Recipe/RecipesGrid.razor.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-stand-database-single-validation-toast.txt" -e=WARNING` — отчёт сформирован; warning по целевому C#-файлу не выявлены.

## Residual Risks

- Интерактивный desktop-прогон в этой сессии не выполнялся, поэтому фактическая последовательность toast-уведомлений подтверждена кодом, но не визуальным UI-прогоном.

## Инциденты

- no new incident
