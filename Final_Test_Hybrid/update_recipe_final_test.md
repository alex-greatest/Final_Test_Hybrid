
# UPDATE запросы для tb_recipe (Final Test)

**База данных:** `traceability_boiler`
**Таблица:** `tb_recipe`
**Фильтр:** `boiler_type_id IN (2, 101) AND station_id IN (501, 502)`

---

## Выполнить запросы

### 1. Time.* теги (16 шт)
```sql
UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."' || REPLACE(tag_name, '.', '"."') || '"',
    is_plc = true
WHERE tag_name LIKE 'Time.%'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);
```

### 2. DHWFlush (без префикса Time)
```sql
UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."Time"."DHWFlush"',
    is_plc = true
WHERE tag_name = 'DHWFlush'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);
```

### 3. Gas.* теги (13 шт)
```sql
UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."' || REPLACE(tag_name, '.', '"."') || '"',
    is_plc = true
WHERE tag_name LIKE 'Gas.%'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);
```

### 4. GasFlow* теги без точек (4 шт)
```sql
UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."Gas"."FlowMin"."SetValue"',
    is_plc = true
WHERE tag_name = 'GasFlowMinSetValue'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."Gas"."FlowMax"."SetValue"',
    is_plc = true
WHERE tag_name = 'GasFlowMaxSetValue'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."Gas"."FlowMax"."UpTol"',
    is_plc = true
WHERE tag_name = 'GasFlowMaxUpTol'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."Gas"."FlowMax"."DownTol"',
    is_plc = true
WHERE tag_name = 'GasFlowMaxDownTol'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);
```

### 5. CH.* теги (16 шт)
```sql
UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."' || REPLACE(tag_name, '.', '"."') || '"',
    is_plc = true
WHERE tag_name LIKE 'CH.%'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);
```

### 6. DHW.* теги (17 шт)
```sql
UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."' || REPLACE(tag_name, '.', '"."') || '"',
    is_plc = true
WHERE tag_name LIKE 'DHW.%'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);
```

### 7. DHW_Flow_Hot_Rate* теги с подчёркиваниями (3 шт)
```sql
UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."DHW"."Flow_Hot_Rate"."Value"',
    is_plc = true
WHERE tag_name = 'DHW_Flow_Hot_Rate'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."DHW"."Flow_Hot_Rate"."Min"',
    is_plc = true
WHERE tag_name = 'DHW_Flow_Hot_Rate_Min'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."DHW"."Flow_Hot_Rate"."Max"',
    is_plc = true
WHERE tag_name = 'DHW_Flow_Hot_Rate_Max'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);
```

### 8. Misc.* теги (2 шт)
```sql
UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."' || REPLACE(tag_name, '.', '"."') || '"',
    is_plc = true
WHERE tag_name LIKE 'Misc.%'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);
```

### 9. Исправления особых тегов (выполнить ПОСЛЕ запросов выше!)

```sql
-- DHW.DHWCheckTankMode: адрес в PLC = Time.DHWCheckTankMode (не DHW!)
UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."Time"."DHWCheckTankMode"',
    is_plc = true
WHERE tag_name = 'DHW.DHWCheckTankMode'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

-- Time.GasLeakTeat: опечатка в tag_name, в PLC = GasLeakTest
UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."Time"."GasLeakTest"',
    is_plc = true
WHERE tag_name = 'Time.GasLeakTeat'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);
```

### 10. Исправление типов данных (2 шт)
```sql
-- DHW.DHWCheckTankMode: REAL -> DINT (в PLC это DInt)
UPDATE tb_recipe
SET plc_type = 'DINT'
WHERE tag_name = 'DHW.DHWCheckTankMode'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

-- DHWFlush: REAL -> DINT (в PLC это DInt)
UPDATE tb_recipe
SET plc_type = 'DINT'
WHERE tag_name = 'DHWFlush'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);
```

### 11. Исправление значений DINT тегов (2 шт)
```sql
-- DHWFlush: 3.0 -> 3 (целое число для DINT)
UPDATE tb_recipe
SET value_ = '3'
WHERE tag_name = 'DHWFlush'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

-- DHW.DHWCheckTankMode: 12.0 -> 12 (целое число для DINT)
UPDATE tb_recipe
SET value_ = '12'
WHERE tag_name = 'DHW.DHWCheckTankMode'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);
```

---

## Проверка результата

```sql
SELECT tag_name, address, is_plc, plc_type
FROM tb_recipe
WHERE boiler_type_id = 2
  AND station_id = 501
  AND is_plc = true
ORDER BY tag_name;
```

---

## Статистика

| Секция | Количество тегов |
|--------|------------------|
| Time.* | 16 |
| Time (особые: DHWFlush, DHW.DHWCheckTankMode) | 2 |
| Gas.* | 13 |
| GasFlow* (без точек) | 4 |
| CH.* | 16 |
| DHW.* | 17 |
| DHW_Flow_Hot_Rate* (подчёркивания) | 3 |
| Misc.* | 2 |
| **Итого** | **73** |

---

## Известные проблемы

| tag_name в базе | Проблема | Решение |
|-----------------|----------|---------|
| `Time.GasLeakTeat` | Опечатка (должно быть GasLeakTest) | Адрес исправлен в запросе 9 |
| `DHW.DHWCheckTankMode` | В PLC находится в Time, не DHW | Адрес исправлен в запросе 9 |

---

## Не найдено в базе данных

| PLC путь | Описание |
|----------|----------|
| `DB_Recipe.DHW.DeltaTemp.Tol` | Допустимое отклонение DeltaTemp для DHW |
