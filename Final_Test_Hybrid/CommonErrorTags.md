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

# Ошибки шагов (Step Errors)

## DHW_Fill_Circuit_Normal_Direction (П-008-xx)

| Код БД | Тег PLC | OPC UA Path | Описание |
|--------|---------|-------------|----------|
| П-008-00 | Al_NoWaterFlow | `ns=3;s="DB_DHW"."DB_DHW_Fill_Circuit_Normal"."Al_NoWaterFlow"` | Нет протока воды |
| П-008-01 | Al_NoWaterPressure | `ns=3;s="DB_DHW"."DB_DHW_Fill_Circuit_Normal"."Al_NoWaterPressure"` | Нет давления воды |
| П-008-02 | Al_FillTime | `ns=3;s="DB_DHW"."DB_DHW_Fill_Circuit_Normal"."Al_FillTime"` | Время заполнения превышено |

---

## Elec - Электрические подключения (П-009-xx)

| Код БД | Описание | Шаг |
|--------|----------|-----|
| П-009-00 | Не подключен силовой кабель | Elec/Connect_Power_Cable |
| П-009-01 | Клипса заземление не подключена | Elec/Connect_Earth_Clip |

> **Примечание:** Это ошибки приложения без PLC тегов

---

## Gas_Leak_Test (П-010-xx)

| Код БД | Тег PLC | OPC UA Path | Описание |
|--------|---------|-------------|----------|
| П-010-00 | Al_LeackGas | `ns=3;s="DB_Gas"."Gas_Leak_Test"."Al_LeackGas"` | Утечка газа |
| П-010-01 | Al_NoPressureGas | `ns=3;s="DB_Gas"."Gas_Leak_Test"."Al_NoPressureGas"` | Нет давления газа |

---

## CH_Fast_Fill_Circuit (П-011-xx)

| Код БД | Тег PLC | OPC UA Path | Описание |
|--------|---------|-------------|----------|
| П-011-00 | Al_NoWaterFlow | `ns=3;s="DB_CH"."DB_CH_Fast_Fill_Circuit"."Al_NoWaterFlow"` | Нет протока воды |
| П-011-01 | Al_NoWaterPressure | `ns=3;s="DB_CH"."DB_CH_Fast_Fill_Circuit"."Al_NoWaterPressure"` | Нет давления воды |
| П-011-02 | Al_FillTime | `ns=3;s="DB_CH"."DB_CH_Fast_Fill_Circuit"."Al_FillTime"` | Время заполнения превышено |

---

## CH_Slow_Fill_Circuit (П-013-xx)

| Код БД | Тег PLC | OPC UA Path | Описание |
|--------|---------|-------------|----------|
| П-013-00 | Al_NoWaterFlow | `ns=3;s="DB_CH"."DB_CH_Slow_Fill_Circuit"."Al_NoWaterFlow"` | Нет протока воды |
| П-013-01 | Al_NoWaterPressure | `ns=3;s="DB_CH"."DB_CH_Slow_Fill_Circuit"."Al_NoWaterPressure"` | Нет давления воды |
| П-013-02 | Al_FillTime | `ns=3;s="DB_CH"."DB_CH_Slow_Fill_Circuit"."Al_FillTime"` | Время заполнения превышено |

---

## Coms - Диагностическая связь (П-016-xx)

| Код БД | Описание | Шаг |
|--------|----------|-----|
| П-016-00 | Нет связи с котлом | Coms/Check_Comms |
| П-016-01 | Ошибка при смене режима котла | Coms/Write_Test_Byte_ON |
| П-016-02 | Котел не в стендовом режиме | Coms/Check_Test_Byte_ON |
| П-016-03 | Ошибка записи в ЭБУ | Coms/Write_Soft_Code_Plug |
| П-016-04 | Ошибка запуска насоса котла | Coms/CH_Pump_Start |

> **Примечание:** Это ошибки приложения без PLC тегов (Modbus диагностика)

---

## Block_Boiler_Adapter (П-086-xx)

| Код БД | Тег PLC | OPC UA Path | Описание |
|--------|---------|-------------|----------|
| П-086-00 | Al_Not_17K4 | `ns=3;s="DB_Common"."Al_Not_17K4"` | Котел не заблокирован |
| П-086-01 | Al_17K5Fault | `ns=3;s="DB_Common"."Al_17K5Fault"` | Реле 17K5 неисправно |

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
(134, 'О-004-00', 'Ошибка записи в ПЛК', 2, 7, NULL),
(135, 'О-004-01', 'Потеря связи с ПЛК', 2, 7, NULL),
(136, 'О-004-02', 'Таймаут чтения тега ПЛК', 2, 7, NULL);

-- DHW_Fill_Circuit_Normal_Direction (П-008-xx) - step_id=TBD
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(137, 'П-008-00', 'DHW_Fill_Circuit_Normal. Неисправность: Нет протока воды', 2, 7, NULL),
(138, 'П-008-01', 'DHW_Fill_Circuit_Normal. Неисправность: Нет давления воды', 2, 7, NULL),
(139, 'П-008-02', 'DHW_Fill_Circuit_Normal. Неисправность: Время заполнения превышено', 2, 7, NULL);

-- Elec (П-009-xx) - step_id=TBD
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(140, 'П-009-00', 'Elec_Connect_Power_Cable. Неисправность: Не подключен силовой кабель', 2, 7, NULL),
(141, 'П-009-01', 'Elec_Connect_Earth_Clip. Неисправность: Клипса заземление не подключена', 2, 7, NULL);

-- Gas_Leak_Test (П-010-xx) - step_id=TBD
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(142, 'П-010-00', 'Gas_Leak_Test. Неисправность: Утечка газа', 2, 7, NULL),
(143, 'П-010-01', 'Gas_Leak_Test. Неисправность: Нет давления газа', 2, 7, NULL);

-- CH_Fast_Fill_Circuit (П-011-xx) - step_id=TBD
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(144, 'П-011-00', 'CH_Fast_Fill_Circuit. Неисправность: Нет протока воды', 2, 7, NULL),
(145, 'П-011-01', 'CH_Fast_Fill_Circuit. Неисправность: Нет давления воды', 2, 7, NULL),
(146, 'П-011-02', 'CH_Fast_Fill_Circuit. Неисправность: Время заполнения превышено', 2, 7, NULL);

-- CH_Slow_Fill_Circuit (П-013-xx) - step_id=TBD
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(147, 'П-013-00', 'CH_Slow_Fill_Circuit. Неисправность: Нет протока воды', 2, 7, NULL),
(148, 'П-013-01', 'CH_Slow_Fill_Circuit. Неисправность: Нет давления воды', 2, 7, NULL),
(149, 'П-013-02', 'CH_Slow_Fill_Circuit. Неисправность: Время заполнения превышено', 2, 7, NULL);

-- Coms (П-016-xx) - step_id=TBD
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(150, 'П-016-00', 'Coms_Check_Comms. Неисправность: Нет связи с котлом', 2, 7, NULL),
(151, 'П-016-01', 'Coms_Write_Test_Byte_ON. Неисправность: Ошибка при смене режима котла', 2, 7, NULL),
(152, 'П-016-02', 'Coms_Check_Test_Byte_ON. Неисправность: Котел не в стендовом режиме', 2, 7, NULL),
(153, 'П-016-03', 'Coms_Write_Soft_Code_Plug. Неисправность: Ошибка записи в ЭБУ', 2, 7, NULL),
(154, 'П-016-04', 'Coms_CH_Pump_Start. Неисправность: Ошибка запуска насоса котла', 2, 7, NULL);

-- Обновить sequence (следующий id = 627)
ALTER SEQUENCE tb_error_settings_template_id_seq RESTART WITH 627;
```
