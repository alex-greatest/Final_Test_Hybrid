# 2026-03-21 interrupt admin auth before reason

## Failure mode

- В soft-reset interrupt-flow для `UseMes=true` действовал временный bypass:
  окно причины прерывания открывалось сразу, без обязательной admin-auth проверки.
- Из-за этого MES interrupt-path отправлял причину с `operator username`, хотя по контракту должен использовать `username` администратора, подтвердившего прерывание.
- `AdminAuthDialog` уже имел server-driven admin contract для rework, но interrupt-flow его не использовал.

## Root cause

- В `PreExecutionCoordinator.Subscriptions` был включён временный компромисс
  `bypassAdminAuthInSoftResetInterrupt = true`.
- `InterruptFlowExecutor` поддерживал двухшаговый flow `auth -> reason`, но этот шаг auth для soft-reset interrupt фактически не активировался.
- `AdminAuthDialog` не имел отдельной штатной cancel-ветки для interrupt-flow с той же инженерной защитой, что уже использовалась в `InterruptReasonDialog`.

## Resolution

- Для `UseMes=true` soft-reset interrupt-path снова требует `AdminAuthDialog` перед открытием окна причины.
- `InterruptFlowExecutor` сохранён как owner двухшагового flow:
  - `AdminAuthDialog`;
  - `InterruptReasonDialog`.
- После успешной admin-auth отправка причины в MES использует `authResult.Username`, а не `OperatorState.Username`.
- `AdminAuthDialog` получил opt-in protected cancel:
  - interrupt-flow включает кнопку `Отмена`;
  - закрытие окна требует инженерный пароль;
  - QR dialog owner освобождается и при success, и при cancel.
- Поведение `UseMes=false` не менялось: direct reason path без admin-auth остался прежним.

## Verification

- Добавлены unit-tests для `InterruptFlowExecutor`:
  - MES path использует `admin username`;
  - закрытие admin-auth окна отменяет flow до открытия окна причины;
  - non-MES path продолжает использовать `operator username`.
- Полный build/format/inspectcode зафиксирован в impact этого change-set.

## Notes

- no new incident
