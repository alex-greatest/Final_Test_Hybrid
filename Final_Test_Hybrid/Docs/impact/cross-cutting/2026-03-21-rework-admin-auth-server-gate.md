# 2026-03-21 rework admin auth server gate

## Контур

- rework-flow / `AdminAuthDialog`
- Spring operator auth / server status gating
- QR scanner dialog ownership без изменения ordinary operator auth

## Что изменено

- `OperatorAuthService` получил отдельные методы и отдельные endpoints для rework admin auth:
  - `AuthenticateAdminAsync(...)`
  - `AuthenticateAdminByQrAsync(...)`
- Новый admin path использует backend routes `/api/admin/auth` и `/api/admin/auth/Qr`, отдельный parser `AdminAuthResponseParser` и больше не вызывает `operatorState.SetAuthenticated(...)`.
- `AdminAuthDialog` переведён на новый admin path и теперь одинаково обрабатывает парольную и QR-ветку по server-driven контракту:
  - `200` -> закрыть dialog как success;
  - `404` -> показать `ErrorResponse.Message` и не переходить к окну причины;
  - `other` -> показать `Неизвестная ошибка` и не переходить к окну причины.
- Локальная проверка пустых полей логина/пароля сохранена; дополнительных role/client-side проверок не добавлялось.
- Stable docs синхронизированы: `PlcResetGuide` теперь явно фиксирует rework admin-auth gate и отсутствие записи в `OperatorState`.

## Контракт и совместимость

- Обычный operator login не менялся:
  - `AuthenticateAsync(...)` и `AuthenticateByQrAsync(...)` продолжают обслуживать основной scan-mode/login flow;
  - запись в `OperatorState` по-прежнему остаётся только в обычном operator success-path.
- Rework admin auth остаётся отдельным UI-gate:
  - success определяется только HTTP-ответом сервера;
  - client-side роль пользователя не проверяется;
  - ввод причины доработки/пропуска открывается только после `200 OK`.
- Разделение route теперь явное:
  - operator login -> `/api/operator/auth`, `/api/operator/auth/Qr`;
  - admin auth -> `/api/admin/auth`, `/api/admin/auth/Qr`.
- QR scanner path сохранён:
  - `AdminAuthDialog` по-прежнему владеет dialog scanner owner;
  - сменился только вызываемый auth method, а не ownership/scanner wiring.

## Затронутые файлы

- `Final_Test_Hybrid/Services/SpringBoot/Operator/OperatorAuthService.cs`
- `Final_Test_Hybrid/Services/SpringBoot/Operator/AdminAuthResponseParser.cs`
- `Final_Test_Hybrid/Components/Main/Modals/Rework/AdminAuthDialog.razor`
- `Final_Test_Hybrid/Docs/runtime/PlcResetGuide.md`
- `Final_Test_Hybrid/Docs/changes/2026-03-21-rework-admin-auth-server-gate.md`
- `Final_Test_Hybrid.Tests/Runtime/AdminAuthResponseParserTests.cs`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx -c Debug "-p:IntermediateOutputPath=%TEMP%\\admin-auth-check4\\obj\\" "-p:OutputPath=%TEMP%\\admin-auth-check4\\bin\\" -p:UseSharedCompilation=false /nr:false` — успешно; baseline warning `MSB3277` по конфликту `WindowsBase` сохранился без новых ошибок.
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~AdminAuthResponseParserTests" -c Debug "-p:IntermediateOutputPath=%TEMP%\\admin-auth-check4\\obj\\" "-p:OutputPath=%TEMP%\\admin-auth-check4\\bin\\"` — успешно, `3/3`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/SpringBoot/Operator/OperatorAuthService.cs;Final_Test_Hybrid/Services/SpringBoot/Operator/AdminAuthResponseParser.cs;Final_Test_Hybrid.Tests/Runtime/AdminAuthResponseParserTests.cs" --no-build --format=Text "--output=.codex-build/inspect-warning-admin-auth.txt" -e=WARNING` — warning по моему diff не осталось.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/SpringBoot/Operator/OperatorAuthService.cs;Final_Test_Hybrid/Services/SpringBoot/Operator/AdminAuthResponseParser.cs" --no-build --format=Text "--output=.codex-build/inspect-hint-admin-auth.txt" -e=HINT` — hint по моему diff не осталось.

## Residual Risks

- Интерактивный WinForms + Blazor Hybrid прогон password/QR rework dialog в этой сессии не выполняется; контракт подтверждается кодом и unit-тестом parser-а.

## Инциденты

- Связанный failure mode зафиксирован в `Docs/changes/2026-03-21-rework-admin-auth-server-gate.md`.
