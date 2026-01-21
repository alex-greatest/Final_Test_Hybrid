# Change: Manual Start + Ping Check for Diagnostic Service

## Why

Текущая реализация имеет две проблемы:
1. **Автостарт** — диспетчер запускается автоматически при первом read/write, что не даёт контроля над моментом подключения
2. **Ложное подключение** — если COM-порт существует, но котёл не подключен, `IsConnected=true` сразу после открытия порта, хотя реально связи нет (первая команда упадёт по таймауту)

## What Changes

- **BREAKING**: Убрать автостарт из `QueuedModbusClient` — требовать явный вызов `IModbusDispatcher.StartAsync()` перед использованием. Все текущие вызовы должны быть обновлены.
- **Добавить ping-проверку** в `ModbusDispatcher.EnsureConnectedAsync()` — после открытия порта читать регистр Firmware Major (адрес вычисляется как `1055 - BaseAddressOffset`), чтобы убедиться что котёл отвечает
- **Обновить документацию** — отразить новое поведение

## Impact

- Affected specs: `diagnostic-service` (новая спека)
- Affected code:
  - `Services/Diagnostic/Protocol/QueuedModbusClient.cs`
  - `Services/Diagnostic/Protocol/CommandQueue/ModbusDispatcher.cs`
  - `Services/Diagnostic/Protocol/CommandQueue/ModbusConnectionManager.cs` (добавить доступ к settings)
  - `Docs/DiagnosticGuide.md`

## Notes

- Ping адрес должен вычисляться динамически: `RegisterFirmwareMajor - BaseAddressOffset` (1055 - 1 = 1054 по умолчанию)
- `InvalidOperationException` будет брошен на уровне `QueuedModbusClient`, до того как `RegisterReader` его поймает, поэтому исключение выйдет наружу
