# 2026-03-23 - BlockA pause until reset

## Summary

- Ветка `BoilerStatus == 2` переведена из `PLC signal stub` в отдельный pause-only interrupt.
- Новый interrupt не выполняет перевод в `Stand`, не пишет `1153=0` и не делает auto-resume по изменению `1005`.
- UI message для этой ветки добавлен отдельно и уточнён для оператора: `Блокировка А. Нажмите сброс`.

## Scope

- Diagnostics runtime: `BoilerLockRuntimeService`
- Execution interrupt handling: `ErrorCoordinator`, `InterruptBehaviorRegistry`, новый `BoilerBlockABehavior`
- Message layer: `MessageServiceResolver`, `MessageTextResources`, `Form1.resx`
- Idle timeout freeze для execution pause

## Changes

1. Добавлен новый `InterruptReason.BoilerBlockA` и pause-only behavior.
2. Ветка `status=2` теперь:
   - поднимает отдельный interrupt;
   - использует общий `PauseToken`;
   - не перехватывает ownership у уже активного interrupt.
3. `AutoReady OFF` не перезаписывает активную pause-ветку `BlockA` или `BoilerLock` на `AutoModeDisabled`, чтобы `AutoReady ON` не снимал общую паузу без нужного runtime-flow/reset.
4. `MessageService` показывает `Блокировка А. Нажмите сброс`, а warning в `BoilerBlockABehavior` синхронизирован с тем же действием; приоритет `Нет связи с PLC` и `Нет автомата` оставлен выше.
5. Ожидание idle исполнителей теперь замораживает таймаут и на `BoilerBlockA`.

## Verification

- `dotnet build Final_Test_Hybrid.slnx`
- `dotnet format analyzers --verify-no-changes`
- `dotnet format style --verify-no-changes`
- точечный `jb inspectcode` warning по изменённым `*.cs`
- точечный `jb inspectcode` hint по изменённым `*.cs`
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~ErrorCoordinatorOwnershipTests|FullyQualifiedName~MessageServiceResolverTests"`

## Docs Updated

- `Docs/diagnostics/BoilerLockGuide.md`
- `Docs/runtime/ErrorCoordinatorGuide.md`

## Incident Status

- no new incident
