## 1. Implementation
- [ ] 1.1 Обновить WaitForResolutionAsync для прямого чтения BlockEnd/BlockError при EnableSkip.
- [ ] 1.2 Обновить WaitForSkipSignalsResetAsync: прямое чтение, ранний выход, расширенный лог при таймауте.
- [ ] 1.3 Добавить таймаут 10с в WaitForExecutorsIdleAsync: лог статусов, пауза + уведомление без NOK.
- [ ] 1.4 Обновить Docs/RetrySkipGuide.md.
- [ ] 1.5 Проверить сценарии: импульс Skip, сброс End/Error, зависание перехода блока.
