# OPC UA Services - План рефакторинга

## Цель

Разбить `OpcUaConnectionService.cs` (462 строки) на несколько файлов согласно:
- Лимиту 300 строк на файл (.cursorrules)
- Принципу Single Responsibility
- Подготовке к добавлению сервисов подписок и чтения/записи

---

## Текущая структура

```
Services/OpcUa/
├── IOpcUaConnectionService.cs   # Интерфейс (24 строки) ✓
├── OpcUaConnectionService.cs    # Реализация (462 строки) ✗ превышает лимит
├── OpcUaSettings.cs             # Настройки (16 строк) ✓
└── refactoring.md               # Документация ✓
```

---

## Целевая структура (Phase 1)

```
Services/OpcUa/
├── IOpcUaConnectionService.cs          # Без изменений
├── OpcUaConnectionService.cs           # Core: lifecycle, public API (~150 строк)
├── OpcUaConnectionService.Reconnect.cs # Partial: reconnect логика (~120 строк)
│
├── IOpcUaSessionFactory.cs             # Новый интерфейс
├── OpcUaSessionFactory.cs              # Создание сессий + конфигурация (~100 строк)
│
├── OpcUaSettings.cs                    # Без изменений
├── OpcUaSettingsValidator.cs           # Валидация настроек (~50 строк)
│
├── refactoring.md                      # Обновить документацию
└── plan.md                             # Этот файл
```

---

## Детальный план

### Шаг 1: Создать IOpcUaSessionFactory

```csharp
public interface IOpcUaSessionFactory
{
    Task<ISession> CreateAsync(OpcUaSettings settings, CancellationToken ct = default);
}
```

**Ответственность:**
- Создание `ApplicationConfiguration`
- Создание `ConfiguredEndpoint`
- Создание `ISession`

### Шаг 2: Создать OpcUaSessionFactory

Извлечь из `OpcUaConnectionService`:
- `CreateApplicationConfigurationAsync()` → приватный метод
- `TryConnectCoreAsync()` → часть логики создания сессии

### Шаг 3: Создать OpcUaSettingsValidator

Извлечь из `OpcUaConnectionService`:
- `ValidateSettings()` → статический метод или отдельный класс

```csharp
public static class OpcUaSettingsValidator
{
    public static void Validate(OpcUaSettings settings, ILogger? logger = null);
}
```

### Шаг 4: Создать OpcUaConnectionService.Reconnect.cs (partial)

Перенести:
- `OnKeepAlive()`
- `HandleKeepAliveError()`
- `StartReconnectHandlerAsync()`
- `OnReconnectComplete()`
- `HandleReconnectCompleteAsync()`
- `UpdateSessionIfReconnected()`
- `IsSessionRecreated()`
- `HandleSessionRecreate()`
- `DisposeReconnectHandler()`
- `RaiseConnectionChangedAsync()`
- `RaiseSessionRecreatedAsync()`

### Шаг 5: Упростить OpcUaConnectionService.cs

Оставить:
- Свойства и поля
- Конструктор
- `StartAsync()`, `StopAsync()`
- `ExecuteWithSessionAsync()` (оба overload)
- `DisposeAsync()`
- `EnsureConnected()`
- `ConnectLoopAsync()`, `WaitForNextTickAsync()`, `TryConnectIfNotConnectedAsync()`
- `TryConnectAsync()` (использует `IOpcUaSessionFactory`)
- `CloseSessionAsync()`
- `WaitForConnectLoopAsync()`

### Шаг 6: Обновить DI регистрацию

```csharp
// Form1.cs
services.AddSingleton<IOpcUaSessionFactory, OpcUaSessionFactory>();
services.AddSingleton<IOpcUaConnectionService, OpcUaConnectionService>();
```

### Шаг 7: Обновить refactoring.md

Добавить информацию о новой структуре.

---

## Будущая структура (Phase 2)

После добавления подписок и read/write:

```
Services/OpcUa/
├── Connection/
│   ├── IOpcUaConnectionService.cs
│   ├── OpcUaConnectionService.cs
│   └── OpcUaConnectionService.Reconnect.cs
│
├── Session/
│   ├── IOpcUaSessionFactory.cs
│   └── OpcUaSessionFactory.cs
│
├── Data/
│   ├── IOpcUaDataService.cs
│   └── OpcUaDataService.cs
│
├── Subscriptions/
│   ├── IOpcUaSubscriptionService.cs
│   └── OpcUaSubscriptionService.cs
│
└── Configuration/
    ├── OpcUaSettings.cs
    └── OpcUaSettingsValidator.cs
```

---

## Граф зависимостей

```
┌─────────────────────────────────────────────────────────┐
│                      DI Container                        │
└─────────────────────────────────────────────────────────┘
                            │
         ┌──────────────────┼──────────────────┐
         ▼                  ▼                  ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ OpcUaSettings   │ │ ILoggerFactory  │ │ ISessionFactory │
│ (IOptions)      │ │                 │ │ (OPC UA SDK)    │
└─────────────────┘ └─────────────────┘ └─────────────────┘
         │                  │                  │
         └──────────────────┼──────────────────┘
                            ▼
                ┌─────────────────────┐
                │ OpcUaSessionFactory │
                └─────────────────────┘
                            │
                            ▼
              ┌───────────────────────┐
              │ OpcUaConnectionService │
              └───────────────────────┘
                            │
         ┌──────────────────┴──────────────────┐
         ▼                                     ▼
┌─────────────────┐                 ┌─────────────────────┐
│ OpcUaDataService│                 │OpcUaSubscriptionSvc │
│   (будущий)     │                 │     (будущий)       │
└─────────────────┘                 └─────────────────────┘
```

---

## Критерии готовности

- [ ] Все файлы ≤ 300 строк
- [ ] Каждый метод имеет максимум один `if`/`for`/`while`/`switch`
- [ ] Нет вложенных блоков
- [ ] Сборка проходит без ошибок
- [ ] Существующий функционал сохранён

---

## Риски

| Риск | Митигация |
|------|-----------|
| Нарушение thread-safety при разбиении | Reconnect остаётся partial class с доступом к приватным полям |
| Увеличение сложности DI | Минимальное кол-во новых интерфейсов |
| Breaking changes в API | IOpcUaConnectionService остаётся без изменений |
