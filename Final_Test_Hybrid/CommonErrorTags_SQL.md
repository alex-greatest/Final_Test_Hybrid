# SQL INSERT для общих ошибок PLC

Добавлено в `traceability_boiler.tb_error_settings_template`

## DB_Message (О-001-xx) - Системные сообщения

```sql
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
```

## DB_Common (О-002-xx) - Общие ошибки

```sql
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(125, 'О-002-00', 'DB_Common. Неисправность: Реле 17K4 неисправно', 2, 7, NULL),
(127, 'О-002-01', 'DB_Common. Неисправность: Котел не разблокирован', 2, 7, NULL);
```

> **Примечание:** Ошибка Al_Not_17K4 (Котел не заблокирован) перенесена к шагу "Block boiler adapter" (см. П-086-00)

## DB_Coms (О-003-xx) - Общие ошибки горения

```sql
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(128, 'О-003-00', 'DB_Coms. Неисправность. Нет протока воды', 2, 7, NULL),
(129, 'О-003-01', 'DB_Coms. Неисправность. Ток ионизации вне допуска', 2, 7, NULL),
(130, 'О-003-02', 'DB_Coms. Неисправность. Стенд не готов', 2, 7, NULL),
(131, 'О-003-03', 'DB_Coms. Неисправность. Время закрытия клапана превышено', 2, 7, NULL);
```

## Block_Boiler_Adapter (П-086-xx) - Ошибки шага

```sql
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(132, 'П-086-00', 'Block_Boiler_Adapter. Неисправность: Котел не заблокирован', 2, 7, 40),
(133, 'П-086-01', 'Block_Boiler_Adapter. Неисправность: Реле 17K5 неисправно', 2, 7, 40);
```

## Ошибки приложения (О-004-xx)

```sql
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(134, 'О-004-00', 'Ошибка записи в ПЛК', 2, 7, NULL),
(135, 'О-004-01', 'Потеря связи с ПЛК', 2, 7, NULL),
(136, 'О-004-02', 'Таймаут чтения тега ПЛК', 2, 7, NULL);
```

---

# Ошибки шагов (Step Errors)

## DHW_Fill_Circuit_Normal_Direction (П-008-xx)

```sql
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(137, 'П-008-00', 'DHW_Fill_Circuit_Normal. Неисправность: Нет протока воды', 2, 7, NULL),
(138, 'П-008-01', 'DHW_Fill_Circuit_Normal. Неисправность: Нет давления воды', 2, 7, NULL),
(139, 'П-008-02', 'DHW_Fill_Circuit_Normal. Неисправность: Время заполнения превышено', 2, 7, NULL);
```

## Elec (П-009-xx)

```sql
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(140, 'П-009-00', 'Elec_Connect_Power_Cable. Неисправность: Не подключен силовой кабель', 2, 7, NULL),
(141, 'П-009-01', 'Elec_Connect_Earth_Clip. Неисправность: Клипса заземление не подключена', 2, 7, NULL);
```

## Gas_Leak_Test (П-010-xx)

```sql
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(142, 'П-010-00', 'Gas_Leak_Test. Неисправность: Утечка газа', 2, 7, NULL),
(143, 'П-010-01', 'Gas_Leak_Test. Неисправность: Нет давления газа', 2, 7, NULL);
```

## CH_Fast_Fill_Circuit (П-011-xx)

```sql
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(144, 'П-011-00', 'CH_Fast_Fill_Circuit. Неисправность: Нет протока воды', 2, 7, NULL),
(145, 'П-011-01', 'CH_Fast_Fill_Circuit. Неисправность: Нет давления воды', 2, 7, NULL),
(146, 'П-011-02', 'CH_Fast_Fill_Circuit. Неисправность: Время заполнения превышено', 2, 7, NULL);
```

## CH_Slow_Fill_Circuit (П-013-xx)

```sql
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(147, 'П-013-00', 'CH_Slow_Fill_Circuit. Неисправность: Нет протока воды', 2, 7, NULL),
(148, 'П-013-01', 'CH_Slow_Fill_Circuit. Неисправность: Нет давления воды', 2, 7, NULL),
(149, 'П-013-02', 'CH_Slow_Fill_Circuit. Неисправность: Время заполнения превышено', 2, 7, NULL);
```

## Coms (П-016-xx)

```sql
INSERT INTO tb_error_settings_template (id, address_error, description, version, station_type_id, step_id)
VALUES
(150, 'П-016-00', 'Coms_Check_Comms. Неисправность: Нет связи с котлом', 2, 7, NULL),
(151, 'П-016-01', 'Coms_Write_Test_Byte_ON. Неисправность: Ошибка при смене режима котла', 2, 7, NULL),
(152, 'П-016-02', 'Coms_Check_Test_Byte_ON. Неисправность: Котел не в стендовом режиме', 2, 7, NULL),
(153, 'П-016-03', 'Coms_Write_Soft_Code_Plug. Неисправность: Ошибка записи в ЭБУ', 2, 7, NULL),
(154, 'П-016-04', 'Coms_CH_Pump_Start. Неисправность: Ошибка запуска насоса котла', 2, 7, NULL),
(627, 'П-016-25', 'Coms_Write_Test_Byte_OFF. Неисправность: Ошибка при выходе из режима Стенд', 2, 7, NULL);
```

---

**Параметры:**
- `station_type_id = 7`
- `version = 2`
- `step_id = NULL` (общие ошибки без привязки к шагу, TBD для шагов)
- `step_id = 40` (Block boiler adapter)
