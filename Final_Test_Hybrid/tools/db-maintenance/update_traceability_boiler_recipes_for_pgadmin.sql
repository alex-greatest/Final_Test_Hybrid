BEGIN;

UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."' || REPLACE(tag_name, '.', '"."') || '"',
    is_plc = true
WHERE tag_name LIKE 'Time.%'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."Time"."DHWFlush"',
    is_plc = true
WHERE tag_name = 'DHWFlush'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."' || REPLACE(tag_name, '.', '"."') || '"',
    is_plc = true
WHERE tag_name LIKE 'Gas.%'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

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

UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."' || REPLACE(tag_name, '.', '"."') || '"',
    is_plc = true
WHERE tag_name LIKE 'CH.%'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."' || REPLACE(tag_name, '.', '"."') || '"',
    is_plc = true
WHERE tag_name LIKE 'DHW.%'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

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

UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."' || REPLACE(tag_name, '.', '"."') || '"',
    is_plc = true
WHERE tag_name LIKE 'Misc.%'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."Time"."DHWCheckTankMode"',
    is_plc = true
WHERE tag_name = 'DHW.DHWCheckTankMode'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET address = 'ns=3;s="DB_Recipe"."Time"."GasLeakTest"',
    is_plc = true
WHERE tag_name = 'Time.GasLeakTeat'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET plc_type = 'DINT'
WHERE tag_name = 'DHW.DHWCheckTankMode'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET plc_type = 'DINT'
WHERE tag_name = 'DHWFlush'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET value_ = '3'
WHERE tag_name = 'DHWFlush'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

UPDATE tb_recipe
SET value_ = '12'
WHERE tag_name = 'DHW.DHWCheckTankMode'
  AND boiler_type_id IN (2, 101)
  AND station_id IN (501, 502);

COMMIT;
