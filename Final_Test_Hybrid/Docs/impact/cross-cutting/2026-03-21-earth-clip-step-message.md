# 2026-03-21 earth clip step-scoped main message

## Контур

- main message source-of-truth
- elec earth clip runtime UX
- step-scoped message ownership

## Что изменено

- Добавлен `EarthClipStepMessageService` как отдельный owner только для lower main message.
- Для `Elec/Connect_Earth_Clip` нижняя строка теперь сразу показывает
  `Подключите клипсу заземления` после перехода шага в phase2 (`Ready_1`).
- Message-state жёстко привязан к active step через `IStepTimingService`:
  - пока шаг `Elec/Connect_Earth_Clip` активен, сообщение может жить;
  - при выходе из шага message скрывается сразу;
  - `SoftStop`, `HardReset`, cancel cleanup и любой другой reset-path глушат message
    через остановку шага и/или потерю active-step ownership.
- Потеря OPC-связи немедленно скрывает earth clip message.
- `ConnectEarthClipStep` оставлен владельцем delayed error path:
  - `Ready_1 + 30 секунд` по-прежнему приводит к
    `RaisePlc(ErrorDefinitions.EarthClipNotConnected)`;
  - existing `ClearPlc(...)` в cleanup phase2 не изменён.
- `MessageService`, `MessageServiceResolver`, `MessageTextResources` и `Form1.resx`
  расширены новым low-priority сценарием earth clip message.
- Stable docs синхронизированы:
  - `Docs/runtime/ErrorSystemGuide.md`
  - `Docs/ui/MessageSemanticsGuide.md`
- `no new incident`: change-set меняет runtime UX нижней строки и не вводит новый
  production failure mode.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Main/Messages/EarthClipStepMessageService.cs`
- `Final_Test_Hybrid/Services/Main/Messages/MessageService.cs`
- `Final_Test_Hybrid/Services/Main/Messages/MessageServiceResolver.cs`
- `Final_Test_Hybrid/Services/Main/Messages/MessageTextResources.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Elec/ConnectEarthClipStep.cs`
- `Final_Test_Hybrid/Services/DependencyInjection/StepsServiceExtensions.cs`
- `Final_Test_Hybrid/Form1.resx`
- `Final_Test_Hybrid.Tests/Runtime/EarthClipStepMessageServiceTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/MessageServiceResolverTests.cs`
- `Final_Test_Hybrid/Docs/runtime/ErrorSystemGuide.md`
- `Final_Test_Hybrid/Docs/ui/MessageSemanticsGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` в рабочей копии — не выполнен: `Final_Test_Hybrid.exe` заблокирован запущенным процессом `Final_Test_Hybrid (61804)`.
- `dotnet build .codex-build\\verify-copy-20260321-earthclip-2\\Final_Test_Hybrid.slnx` — успешно; сохранены baseline warnings `MSB3277` по конфликту `WindowsBase`.
- `dotnet test .codex-build\\verify-copy-20260321-earthclip-2\\Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~EarthClipStepMessageServiceTests|FullyQualifiedName~MessageServiceResolverTests"` — успешно, `23/23`.
- `dotnet format .codex-build\\verify-copy-20260321-earthclip-2\\Final_Test_Hybrid.slnx analyzers --verify-no-changes` — успешно.
- `dotnet format .codex-build\\verify-copy-20260321-earthclip-2\\Final_Test_Hybrid.slnx style --verify-no-changes` — успешно.
- `jb inspectcode .codex-build\\verify-copy-20260321-earthclip-2\\Final_Test_Hybrid.slnx --include=<changed.cs>` `-e=WARNING` — новых warning по изменённым `*.cs` нет.
- `jb inspectcode .codex-build\\verify-copy-20260321-earthclip-2\\Final_Test_Hybrid.slnx --include=<changed.cs>` `-e=HINT` — только stylistic suggestions в `MessageServiceResolver.cs` и ожидаемый false positive `ConnectEarthClipStep` как reflection-created step; runtime-рисков не найдено.
- `powershell -ExecutionPolicy Bypass -File C:\\Users\\Alexander\\.codex\\skills\\localization-sync-guard\\scripts\\replay_localization_sync.ps1 -RepoRoot .codex-build\\verify-copy-20260321-earthclip-2 -RequireResourceSync -RequireCyrillicLogs` — успешно.

## Инциденты

- no new incident
