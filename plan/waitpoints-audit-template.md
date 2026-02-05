# РЁР°Р±Р»РѕРЅ Р°СѓРґРёС‚Р° waitpoints (РѕР¶РёРґР°РЅРёР№/СЃРёРіРЅР°Р»РѕРІ) вЂ” Final_Test_Hybrid

## РљР°Рє РїРѕР»СЊР·РѕРІР°С‚СЊСЃСЏ
- РћРґРЅР° СЃС‚СЂРѕРєР° = РѕРґРёРЅ вЂњwaitpointвЂќ (РѕР¶РёРґР°РЅРёРµ/Р±Р»РѕРєРёСЂРѕРІРєР°/СЃРёРіРЅР°Р»/Р±Р°СЂСЊРµСЂ).
- Р”РѕР±Р°РІР»СЏР№С‚Рµ СЃС‚СЂРѕРєРё РїРѕ РјРµСЂРµ РѕР±РЅР°СЂСѓР¶РµРЅРёСЏ.
- РџСЂРёРѕСЂРёС‚РµС‚С‹:
  - **P0** вЂ” РјРѕР¶РµС‚ РїСЂРёРІРµСЃС‚Рё Рє Р·Р°РІРёСЃР°РЅРёСЋ / unsafeвЂ‘СЃРѕСЃС‚РѕСЏРЅРёСЋ / РїРѕС‚РµСЂРµ СЂРµР·СѓР»СЊС‚Р°С‚Р°
  - **P1** вЂ” РїСЂРёРІРѕРґРёС‚ Рє РЅРµРІРµСЂРЅРѕРјСѓ РїРѕРІРµРґРµРЅРёСЋ / С„Р»РµР№РєР°Рј / С‚СЂСѓРґРЅРѕРѕС‚Р»Р°РІР»РёРІР°РµРјС‹Рј РіРѕРЅРєР°Рј
  - **P2** вЂ” СѓР»СѓС‡С€РµРЅРёСЏ РєР°С‡РµСЃС‚РІР°/РґРёР°РіРЅРѕСЃС‚РёРєРё/РїРѕРґРґРµСЂР¶РёРІР°РµРјРѕСЃС‚Рё

Р РµРєРѕРјРµРЅРґСѓРµРјР°СЏ РїСЂР°РєС‚РёРєР°: СЃРЅР°С‡Р°Р»Р° Р·Р°РїРѕР»РЅРёС‚СЊ С‚Р°Р±Р»РёС†Сѓ РЅР° 70вЂ“80% (РїРѕ РІСЃРµРј РєР»СЋС‡РµРІС‹Рј Р·РѕРЅР°Рј), Р·Р°С‚РµРј РЅР°С‡Р°С‚СЊ РїСЂР°РІРєРё РїРѕ С„Р°Р·Р°Рј, Рё РїРѕСЃР»Рµ РєР°Р¶РґРѕР№ С„Р°Р·С‹ РѕР±РЅРѕРІР»СЏС‚СЊ СЃС‚Р°С‚СѓСЃ waitpoints (РёСЃРїСЂР°РІР»РµРЅРѕ/СЃРјСЏРіС‡РµРЅРѕ/РѕСЃС‚Р°РІР»РµРЅРѕ РєР°Рє РµСЃС‚СЊ).

---

## РўР°Р±Р»РёС†Р° waitpoints

| ID | Priority | Area | Waitpoint (method) | File | Р–РґС‘Рј (РёРЅРІР°СЂРёР°РЅС‚) | РСЃС‚РѕС‡РЅРёРє СЃРёРіРЅР°Р»Р° | Timeout | Cancel path | РџСЂРё timeout/cancel | Р§С‚Рѕ Р»РѕРіРёСЂРѕРІР°С‚СЊ | Р РёСЃРєРё | Notes / Repro |
|---:|:--------:|------|--------------------|------|------------------|------------------|---------|------------|-------------------|----------------|------|--------------|
| 1 | P0 | Execution | `WaitForExecutorsIdleAsync` | `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.Execution.Helpers.cs` | все колонки execution-idle | `IsExecutionIdle` | нет | map token (`_cts.Token`) | OCE | start/end wait + correlation ids | hang на переходе Map | зависит от execution-idle |
| 2 | P0 | Execution | `WaitForFinalResolutionIfNeededAsync` | `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.Waitpoints.cs` | последний шаг резолвлен (Retry/Skip) | внутреннее состояние + resolution gate | нет | map token | OCE | start/end wait + HasFailed/StepName | hang после позднего Retry | финальный барьер |
| 3 | P1 | Execution | `WaitForMapAccessAsync` | `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.Waitpoints.cs` | `_mapGate` открыт и snapshot совпал | gate + snapshot | нет | map token | OCE | rate-limited gate blocked + correlation ids | recursion risk removed | loop + rate limit |
| 4 | P0 | ErrorRes | `ErrorCoordinator.WaitForResolutionAsync` | `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.Resolution.cs` | решение Retry/Skip | subscription waitgroup (+direct read precheck) | опц. | ct | Timeout→Timeout | start/end wait + winner + tag snapshot | пропуск импульса, pause-семантика | waitpoint logs added |
| 5 | P0 | Completion | `HandleTestCompletedAsync` (ожидание End=false) | `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Completion/TestCompletionCoordinator.Flow.cs` | PLC сбросил End | subscription | 60s | ct | TagTimeout + Stop | start/end wait + End snapshot | бесконечное ожидание | bounded timeout |
| 6 | P1 | PreExec | `WaitForBarcodeAsync` | `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.MainLoop.cs` | РїРѕР»СѓС‡РµРЅ barcode | TCS + reset CTS | РЅРµС‚ | linked ct | cancel & retry | reset sequence snapshot | вЂњР·Р°Р»РёРївЂќ РІРІРѕРґР° | РїСЂРѕРІРµСЂРёС‚СЊ lifecycle |
| 7 | P1 | Infra | `TagWaiter.WaitWithTimeoutAsync` | `Final_Test_Hybrid/Services/OpcUa/TagWaiter.cs` | СѓСЃР»РѕРІРёРµ РїРѕ С‚РµРіСѓ | subscription callbacks | Р·Р°РІРёСЃРёС‚ | pause-aware / ct | Timeout/OCE | elapsed/remaining + pause transitions | РЅРµРїСЂР°РІРёР»СЊРЅС‹Р№ СѓС‡С‘С‚ РІСЂРµРјРµРЅРё | audit РЅР° edge-cases |
| 8 | P1 | ErrorQueue | `ProcessErrorSignalsAsync` | `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorSignals.cs` | СЃРёРіРЅР°Р» РѕР± РѕС€РёР±РєРµ | bounded channel DropWrite | РЅРµС‚ | token | OCE | РёРЅРІР°СЂРёР°РЅС‚: HasPendingErrorsв†’РѕР±СЂР°Р±РѕС‚РєР° | вЂњРѕС€РёР±РєР° Р±РµР· РѕР±СЂР°Р±РѕС‚РєРёвЂќ | РґРѕР±Р°РІРёС‚СЊ backup trigger |
| 9 | P2 | Interrupts | `HandleInterruptAsync` | `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.Interrupts.cs` | acquired lock | semaphore | РЅРµС‚ | ct | return false | acquire/skip reason | вЂњС‚РёС…Рѕ РїСЂРѕРїСѓСЃС‚РёР»Рё interruptвЂќ | РјРµС‚СЂРёРєРё/Р»РѕРіРё |

_Р”РѕР±Р°РІР»СЏР№С‚Рµ СЃС‚СЂРѕРєРё РїРѕ РјРµСЂРµ РѕР±РЅР°СЂСѓР¶РµРЅРёСЏ._

---

## Correlation IDs (РјРёРЅРёРјР°Р»СЊРЅС‹Р№ РЅР°Р±РѕСЂ)
- `TestRunId`
- `MapIndex`, `MapRunId`
- `ColumnIndex`
- `UiStepId`
- `StepName`
- `PlcBlockPath` (РµСЃР»Рё РµСЃС‚СЊ)

---

## РЎС‚Р°РЅРґР°СЂС‚РёР·РёСЂРѕРІР°РЅРЅС‹Рµ Р»РѕРіРё РґР»СЏ timeout/stuck
РџСЂРё Р»СЋР±РѕРј timeout/stuck СЃРѕР±С‹С‚РёРё Р»РѕРіРёСЂСѓРµРј:
- С‚РµРєСѓС‰РёР№ snapshot СЃРѕСЃС‚РѕСЏРЅРёСЏ (StateManager/FlowState/Paused/StopRequested)
- Р·РЅР°С‡РµРЅРёСЏ PLCвЂ‘С‚РµРіРѕРІ: subscription + direct read (РµСЃР»Рё РїСЂРёРјРµРЅРёРјРѕ)
- СЃРѕСЃС‚РѕСЏРЅРёРµ gateвЂ™РѕРІ (РµСЃР»Рё РїСЂРёРјРµРЅРёРјРѕ)
- Р°РєС‚РёРІРЅС‹Рµ РєРѕР»РѕРЅРєРё/С€Р°РіРё (РґР»СЏ map transition)




