# Scanner Guide

## Архитектура

Система сканирования использует Windows Raw Input API для получения данных напрямую от сканера штрих-кодов, минуя стандартную обработку клавиатуры.

### Компоненты

| Компонент | Описание |
|-----------|----------|
| `RawInputService` | Координатор — регистрация, обработка WM_INPUT, диспетчеризация |
| `RawInputMessageFilter` | IMessageFilter для перехвата WM_INPUT в message loop |
| `ScannerDeviceDetector` | Определение целевого сканера по VID/PID |
| `BarcodeBuffer` | Накопление символов штрих-кода |
| `ScannerInputOwnershipService` | Единый router ownership для `PreExecution` и scanner-диалогов |
| `BarcodeDebounceHandler` | Дебаунс и валидация штрих-кода перед PreExecution + гейтинг по `IsAcceptingInput` и `OpcUaConnectionState.IsConnected` |
| `KeyboardInputProcessor` | Обработка клавиатурных событий |
| `KeyboardInputMapper` | Маппинг vKey → символ с учётом Shift |
| `ScannerConnectionState` | Мониторинг подключения/отключения через WMI |

### Поток данных

1. Сканер отправляет HID keyboard events
2. Windows генерирует WM_INPUT
3. `RawInputMessageFilter.PreFilterMessage()` перехватывает
4. `RawInputService.ProcessRawInput()` читает данные
5. `ScannerDeviceDetector` проверяет VID/PID
6. `KeyboardInputProcessor` определяет действие
7. `BarcodeBuffer` накапливает символы
8. При Enter — barcode готов
9. `ScannerInputOwnershipService` выбирает owner: `Dialog`, `PreExecution` или `None`
10. Для `PreExecution` owner `BarcodeDebounceHandler` дебаунсит и валидирует barcode
11. Валидный штрих-код передаётся в `PreExecutionCoordinator`

## Ownership barcode-ввода

Система использует единый ownership-контракт для raw scanner:

- `PreExecution` owner — обычный scan-mode;
- `Dialog` owner — явный scanner-dialog (`UserScanDialog`, QR-ветка `AdminAuthDialog`);
- `None` — scanner не должен доставлять barcode ни в один runtime-поток.

Правила:

- raw scanner больше не должен молча зависеть от случайного активного handler-а;
- barcode маршрутизируется только через `ScannerInputOwnershipService`;
- при отсутствии owner barcode не теряется молча: пишется диагностика `barcode_rejected_no_owner`.

## Гейтинг ввода barcode

Для pre-execution barcode принимается только если выполняются оба условия:

- `PreExecutionCoordinator.IsAcceptingInput == true`
- `OpcUaConnectionState.IsConnected == true`

Дополнительный контракт синхронизации UI и raw scanner:

- если `BoilerInfo` editable и принимает `Enter`, raw scanner обязан идти в тот же `PreExecution` pipeline;
- если `BoilerInfo` заблокирован, raw scanner не должен запускать `PreExecution`;
- активный `Dialog` owner делает `BoilerInfo` неготовым для обычного scan-mode.

Это правило применяется для обоих каналов:

| Канал | Поведение при `IsConnected = false` |
|-------|-------------------------------------|
| Ручной ввод (`BoilerInfo`) | Поле read-only, Enter не отправляет barcode |
| Аппаратный сканер (`RawInput` → `BarcodeDebounceHandler`) | Штрих-код игнорируется до восстановления связи |

Диагностический контракт для аппаратного сканера:
- при аппаратном barcode-drop `BarcodeDebounceHandler` пишет debug-лог `barcode_drop`;
- причина фиксируется в поле `reason`:
  `not_accepting_input` или `opc_disconnected`;
- лог также содержит текущие значения `isAcceptingInput` и `isConnected`.

Дополнительный контракт ручного ввода:
- если после soft reset поле `BoilerInfo` сохраняет предыдущий barcode/серийный номер видимым и затем снова становится editable, это видимое значение считается активным draft-содержимым поля;
- в таком состоянии `Enter` обязан повторно отправлять сохранённый barcode без обязательного стирания или редактирования хотя бы одного символа;
- повторные `StateHasChanged`/gating refresh без изменения сохранённого значения не должны затирать ручную правку поля обратно в старый barcode.

Дополнительно для UI-тайминга:
- Если система находится в ожидании barcode (`IsAcceptingInput = true`) и PLC-связь пропала, Scan-таймер ставится на паузу.
- После восстановления PLC-связи Scan-таймер продолжает тикать только если scan-mode остаётся активным и нет reset-фазы.
- После `PlcConnectionLost -> HardReset -> Reconnect` barcode re-arm должен происходить через новый цикл `WaitForBarcodeAsync`; визуальная активность `BoilerInfo` не является достаточным доказательством готовности raw scanner pipeline.
- При PLC reset scanner ownership снимается централизованно; при non-PLC hard reset активный dialog-owner должен сниматься немедленно, но возврат в обычный `PreExecution` flow всё равно определяется `IsAcceptingInput`/reset lifecycle, а не фактом закрытия окна.

## Конфигурация

```json
{
  "Scanner": {
    "VendorId": "1FBB",
    "ProductId": "3681"
  }
}
```

## Известные проблемы

### Шумовые короткие сканы

**Симптом:** В гриде кратковременно появляется ошибка «штрих-код слишком короткий» при шумовом вводе.

**Решение:** В режиме ожидания применяется дебаунс ~250 мс. Ошибка показывается только если короткий скан остался последним за окно.

### Потеря первого символа при пробуждении сканера

**Симптом:** Первый символ штрих-кода теряется если сканер не использовался несколько секунд.

**Причина:** Сканер имеет внутренний режим энергосбережения (power saving в firmware). При пробуждении первый символ теряется на уровне драйвера/Windows ДО того как WM_INPUT генерируется.

**Доказательства:**
- Проблема воспроизводится в любом приложении (включая Notepad)
- При быстром повторном сканировании (до засыпания) — проблемы нет
- vKey для первого символа не появляется в WM_INPUT логах

**Workarounds:**
1. Настроить сканер отключить auto-sleep (через утилиту производителя)
2. Добавить prefix-символ в настройках сканера (будет потерян при пробуждении)
3. Использовать сканер без агрессивного power saving

