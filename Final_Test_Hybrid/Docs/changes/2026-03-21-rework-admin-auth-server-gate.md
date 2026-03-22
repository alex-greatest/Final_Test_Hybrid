# 2026-03-21 rework admin auth server gate

## Failure mode

- Диалог `Авторизация администратора` в rework-flow использовал общий `OperatorAuthService.Authenticate*`.
- При `200 OK` этот общий путь не только возвращал success, но и записывал пользователя в `OperatorState`.
- Из-за этого rework admin auth имел побочный эффект обычной авторизации оператора и не был выделен как отдельный server-driven gate.
- Контракт обработки статусов для rework admin auth не был зафиксирован отдельно: диалог не отличал свой success-path от обычного operator login.

## Root cause

- `AdminAuthDialog` вызывал `AuthenticateAsync` / `AuthenticateByQrAsync`, рассчитанные на основной operator login.
- Даже после выделения отдельного client-side admin path запрос по ошибке продолжал идти в обычные operator routes `/api/operator/auth` и `/api/operator/auth/Qr`.
- Для backend-контракта rework admin auth нужны отдельные routes `/api/admin/auth` и `/api/admin/auth/Qr`; только на них сервер возвращает ожидаемый `404` для не-админа.
- В `OperatorAuthService.HandleSuccessAsync(...)` success-path обычного login всегда вызывал `operatorState.SetAuthenticated(...)`.
- Отдельного parser/gate для rework admin auth с контрактом `200/404/other` не существовало.

## Resolution

- В `OperatorAuthService` добавлен отдельный admin-auth path для rework dialog:
  - `AuthenticateAdminAsync(...)`;
  - `AuthenticateAdminByQrAsync(...)`.
- Новый path использует отдельные серверные endpoints `/api/admin/auth` и `/api/admin/auth/Qr` и parser `AdminAuthResponseParser`.
- Контракт зафиксирован жёстко:
  - `200` -> success и переход к окну причины;
  - `404` -> остаёмся в admin dialog и показываем `ErrorResponse.Message`;
  - любой другой статус -> остаёмся в admin dialog и показываем `Неизвестная ошибка`.
- Успешная rework admin auth больше не пишет пользователя в `OperatorState`.
- Локальная проверка пустых логина/пароля в `AdminAuthDialog` сохранена; role/client-side тип пользователя не проверяются.

## Verification

- Добавлен unit-test на parser статусов:
  - `200` -> success с username;
  - `404` -> known error с server message;
  - `other` -> unknown error.
- Полный build/test/formatter/inspectcode зафиксированы в impact этого change-set.

## Notes

- no new incident
