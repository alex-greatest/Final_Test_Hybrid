# Общие теги ошибок PLC

Теги общих ошибок, не привязанных к конкретному тестовому шагу.

## Формат OPC UA путей

- Простой: `ns=3;s="TagName"`
- Вложенный: `ns=3;s="DB_Name"."Field"`
- **Массив: `ns=3;s="DB_Name"."Array"[index]`** (индекс ПОСЛЕ кавычек!)

---

## DB_Message - Системные сообщения (О-001-xx)

| Код БД | Тег PLC | OPC UA Path | Описание |
|--------|---------|-------------|----------|
| О-001-00 | Alarm4[2] | `ns=3;s="DB_Message"."Alarm4"[2]` | Управление не включено |
| О-001-01 | Alarm4[3] | `ns=3;s="DB_Message"."Alarm4"[3]` | Нет режима |
| О-001-02 | Alarm4[4] | `ns=3;s="DB_Message"."Alarm4"[4]` | Селектор выбора режима |
| О-001-03 | Alarm4[5] | `ns=3;s="DB_Message"."Alarm4"[5]` | Ошибка Profibus |
| О-001-04 | Alarm4[6] | `ns=3;s="DB_Message"."Alarm4"[6]` | Нет подачи воздуха |
| О-001-05 | Alarm4[7] | `ns=3;s="DB_Message"."Alarm4"[7]` | Не включен один из автоматов питания |
| О-001-06 | Alarm4[8] | `ns=3;s="DB_Message"."Alarm4"[8]` | Нажата кнопка "Стоп подачи газа" |
| О-001-07 | Alarm4[9] | `ns=3;s="DB_Message"."Alarm4"[9]` | Нажата кнопка "Выключение автоматического цикла" |

---

## DB_Common - Общие ошибки (О-002-xx)

| Код БД | Тег PLC | OPC UA Path | Описание |
|--------|---------|-------------|----------|
| О-002-00 | Al_17K4Fault | `ns=3;s="DB_Common"."Al_17K4Fault"` | Реле 17K4 неисправно |
| О-002-01 | Al_Not_17K5 | `ns=3;s="DB_Common"."Al_Not_17K5"` | Котел не разблокирован |

> **Примечание:** Ошибка Al_Not_17K4 (Котел не заблокирован) перенесена к шагу "Block boiler adapter" (П-086-00)

---

## DB_Coms - Общие ошибки горения (О-003-xx)

| Код БД | Тег PLC | OPC UA Path | Описание |
|--------|---------|-------------|----------|
| О-003-00 | Al_NoWaterFlow | `ns=3;s="DB_Coms"."Al_NoWaterFlow"` | Нет протока воды |
| О-003-01 | Al_IonCurrentOutTol | `ns=3;s="DB_Coms"."Al_IonCurrentOutTol"` | Ток ионизации вне допуска |
| О-003-02 | Al_NotStendReady | `ns=3;s="DB_Coms"."Al_NotStendReady"` | Стенд не готов |
| О-003-03 | Al_CloseTime | `ns=3;s="DB_Coms"."Al_CloseTime"` | Время закрытия клапана превышено |

---

## Ошибки приложения (О-004-xx)

| Код БД | Описание |
|--------|----------|
| О-004-00 | Ошибка записи в ПЛК |
| О-004-01 | Потеря связи с ПЛК |
| О-004-02 | Таймаут чтения тега ПЛК |

---

## Использование в коде

```csharp
using Final_Test_Hybrid.Models.Plc.Tags;

// Чтение тега
var value = await opcUaService.ReadAsync<bool>(CommonErrorTags.Common_BoilerNotLocked);

// Подписка на изменения
await subscriptionService.SubscribeAsync(CommonErrorTags.Message_ProfibusError, OnErrorChanged);
```

---

## SQL для добавления в БД

```sql
-- DB_Message (О-001-xx)
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(119, 'О-001-00', 'DB_Message. Неисправность Управление не включено', 2, 7, NULL),
(120, 'О-001-01', 'DB_Message. Неисправность нет режима', 2, 7, NULL),
(121, 'О-001-02', 'DB_Message. Неисправность селектора выбора режима', 2, 7, NULL),
(122, 'О-001-03', 'DB_Message. Неисправность ошибка Profibus', 2, 7, NULL),
(123, 'О-001-04', 'DB_Message. Неисправность Нет подачи воздуха', 2, 7, NULL),
(624, 'О-001-05', 'DB_Message. Неисправность Не включен один из автоматов питания', 2, 7, NULL),
(625, 'О-001-06', 'DB_Message. Неисправность. Нажата кнопка "Стоп подачи газа"', 2, 7, NULL),
(626, 'О-001-07', 'DB_Message. Неисправность. Нажата кнопка "Выключение автоматического цикла"', 2, 7, NULL);

-- DB_Common (О-002-xx) - общие ошибки
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(125, 'О-002-00', 'DB_Common. Неисправность: Реле 17K4 неисправно', 2, 7, NULL),
(127, 'О-002-01', 'DB_Common. Неисправность: Котел не разблокирован', 2, 7, NULL);

-- Block_Boiler_Adapter (П-086-xx) - ошибки шага (step_id=40)
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(132, 'П-086-00', 'Block_Boiler_Adapter. Неисправность: Котел не заблокирован', 2, 7, 40),
(133, 'П-086-01', 'Block_Boiler_Adapter. Неисправность: Реле 17K5 неисправно', 2, 7, 40);

-- DB_Coms общие (О-003-xx)
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(128, 'О-003-00', 'DB_Coms. Неисправность. Нет протока воды', 2, 7, NULL),
(129, 'О-003-01', 'DB_Coms. Неисправность. Ток ионизации вне допуска', 2, 7, NULL),
(130, 'О-003-02', 'DB_Coms. Неисправность. Стенд не готов', 2, 7, NULL),
(131, 'О-003-03', 'DB_Coms. Неисправность. Время закрытия клапана превышено', 2, 7, NULL);

-- Ошибки приложения (О-004-xx)
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(134, 'О-004-00', 'Ошибка записи в ПЛК', 2, 7, NULL);

INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
    (135, 'О-004-01', 'Потеря связи с ПЛК', 2, 7, NULL);

INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
    (136, 'О-004-02', 'Таймаут чтения тега ПЛК', 2, 7, NULL);

-- Обновить sequence (следующий id = 627)
ALTER SEQUENCE tb_error_settings_template_id_seq RESTART WITH 627;
```
