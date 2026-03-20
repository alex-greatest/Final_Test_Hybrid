# MessageServiceDescription.md

## Статус

Этот файл исторический и не является source-of-truth.

## Где смотреть актуальное поведение

- `Final_Test_Hybrid/Docs/ui/MessageSemanticsGuide.md` — приоритеты сценариев и тексты нижней строки.
- `Final_Test_Hybrid/Docs/runtime/ErrorCoordinatorGuide.md` — ownership interrupt-ов и toast-path.
- `Final_Test_Hybrid/Docs/runtime/PlcResetGuide.md` — reset/post-AskEnd semantics для main message.
- `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md` — роль `RuntimeTerminalState` и его consumers.

## Почему файл выведен из активной документации

Актуальная реализация больше не использует старую таблицу raw priority-rules и не описывается отдельным массивом правил внутри `MessageService`.

Новая модель опирается на:

- snapshot/scenario resolver;
- resource-backed message texts через `MessageTextResources` и `Form1.resx`;
- terminal ownership через `RuntimeTerminalState`;
- reason-specific interrupt semantics;
- отдельный pending-reset narrative для `PlcConnectionLost`.
