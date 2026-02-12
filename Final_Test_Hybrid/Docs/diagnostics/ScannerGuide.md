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
9. ScanModeController + BarcodeDebounceHandler дебаунсит и валидирует
10. Валидный штрих-код передаётся в PreExecutionCoordinator

## Гейтинг ввода barcode

Для pre-execution barcode принимается только если выполняются оба условия:

- `PreExecutionCoordinator.IsAcceptingInput == true`
- `OpcUaConnectionState.IsConnected == true`

Это правило применяется для обоих каналов:

| Канал | Поведение при `IsConnected = false` |
|-------|-------------------------------------|
| Ручной ввод (`BoilerInfo`) | Поле read-only, Enter не отправляет barcode |
| Аппаратный сканер (`RawInput` → `BarcodeDebounceHandler`) | Штрих-код игнорируется до восстановления связи |

Дополнительно для UI-тайминга:
- Если система находится в ожидании barcode (`IsAcceptingInput = true`) и PLC-связь пропала, Scan-таймер ставится на паузу.
- После восстановления PLC-связи Scan-таймер продолжает тикать только если scan-mode остаётся активным и нет reset-фазы.

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

