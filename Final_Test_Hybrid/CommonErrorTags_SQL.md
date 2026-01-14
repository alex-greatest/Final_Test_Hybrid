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
(123, 'О-001-04', 'DB_Message. Неисправность Нет подачи воздуха', 2, 7, NULL);
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
(134, 'О-004-00', 'Ошибка записи в ПЛК', 2, 7, NULL);
```

---

**Параметры:**
- `station_type_id = 7`
- `version = 2`
- `step_id = NULL` (общие ошибки без привязки к шагу)
- `step_id = 40` (Block boiler adapter)
