-- reseed_traceability_boiler_errors_from_program.sql
-- Источник: Final_Test_Hybrid/Models/Errors/ErrorDefinitions*.cs
-- Назначение: traceability_boiler (tb_error_settings_template + tb_error_settings_history)
-- Важно: запускать в БД traceability_boiler.
-- Поведение:
-- 1) Деактивирует текущие active history для выбранного station_type_id.
-- 2) Удаляет шаблоны выбранного station_type_id.
-- 3) Вставляет полный набор ошибок из программы.
-- 4) Создаёт active history-записи в схеме traceability_boiler.
-- 5) Fail-fast при несопоставленных/неоднозначных шагах.

BEGIN;

DO $$
DECLARE
    v_station_type_id bigint := 7;
    v_version bigint := 2;
    v_missing_steps text;
    v_ambiguous_steps text;
    v_template_seq text;
    v_history_seq text;
BEGIN
    CREATE TEMP TABLE src_errors(
        code text NOT NULL,
        description text NULL,
        related_step_name text NULL
    ) ON COMMIT DROP;

    INSERT INTO src_errors(code, description, related_step_name)
    VALUES
    ('О-001-00', 'Управление не включено', NULL),
    ('О-001-01', 'Нет режима', NULL),
    ('О-001-02', 'Селектор выбора режима', NULL),
    ('О-001-03', 'Ошибка Profibus', NULL),
    ('О-001-04', 'Нет подачи воздуха', NULL),
    ('О-001-05', 'Не включен один из автоматов питания', NULL),
    ('О-001-06', 'Нажата кнопка \"Стоп подачи газа\"', NULL),
    ('О-001-07', 'Нажата кнопка \"Выключение автоматического цикла\"', NULL),
    ('О-002-00', 'Реле 17K4 неисправно', NULL),
    ('О-002-01', 'Котел не разблокирован', NULL),
    ('О-003-00', 'Нет протока воды', NULL),
    ('О-003-01', 'Ток ионизации вне допуска', NULL),
    ('О-003-02', 'Стенд не готов', NULL),
    ('О-003-03', 'Время закрытия клапана превышено', NULL),
    ('О-004-00', 'Ошибка записи в ПЛК', NULL),
    ('О-004-01', 'Потеря связи с ПЛК', NULL),
    ('О-004-02', 'Таймаут чтения тега ПЛК', NULL),
    ('О-005-00', 'Неисправность реле 6K1', NULL),
    ('О-005-01', 'Неисправность реле 6K2', NULL),
    ('О-005-02', 'Неисправность изоляции', NULL),
    ('О-005-03', 'Неисправность. Напряжение меньше допустимого', NULL),
    ('О-005-04', 'Неисправность. Напряжение больше допустимого', NULL),
    ('О-005-05', 'Неисправность. Адаптер не вставлен', NULL),
    ('П-100-00', 'Нет связи с котлом', 'Coms/Check_Comms'),
    ('П-101-00', 'Ошибка при смене режима котла', 'Coms/Write_Test_Byte_ON'),
    ('П-102-00', 'Ошибка при выходе из режима Стенд', 'Coms/Write_Test_Byte_OFF'),
    ('П-103-00', 'Котел не в стендовом режиме', 'Coms/Check_Test_Byte_ON'),
    ('П-104-00', 'Ошибка записи в ЭБУ', 'Coms/Write_Soft_Code_Plug'),
    ('П-105-00', 'Ошибка запуска насоса котла', 'Coms/CH_Pump_Start'),
    ('П-106-00', 'Несовпадение артикула в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-01', 'Несовпадение типа котла в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-02', 'Несовпадение типа насоса в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-03', 'Несовпадение типа датчика давления в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-04', 'Несовпадение типа регулятора газа в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-05', 'Несовпадение макс. теплопроизводительности отопления в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-06', 'Несовпадение макс. теплопроизводительности ГВС в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-07', 'Несовпадение мин. теплопроизводительности отопления в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-08', 'Несовпадение режима работы насоса в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-09', 'Несовпадение установленной мощности насоса в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-10', 'Несовпадение вида газа в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-11', 'Несовпадение сдвига тока на модуляционной катушке в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-12', 'Несовпадение коэффициента k расхода воды в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-13', 'Несовпадение макс. мощности насоса в авто режиме в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-14', 'Несовпадение мин. мощности насоса в авто режиме в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-15', 'Несовпадение гистерезиса ГВС в режиме комфорт в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-16', 'Несовпадение макс. температуры подающей линии в ЭБУ', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-17', 'Не установлена перемычка термостата', 'Coms/Read_Soft_Code_Plug'),
    ('П-106-18', 'Несовпадение типа подключения к котлу (1054)', 'Coms/Read_Soft_Code_Plug'),
    ('П-107-00', 'Неверная версия ПО', 'Coms/Read_ECU_Version'),
    ('П-108-00', 'Котел все еще в режиме Стенд', 'Coms/Check_Test_Byte_OFF'),
    ('П-109-00', 'Неисправность. Нет протока воды', 'Coms/CH_Start_Max_Heatout'),
    ('П-109-01', 'Неисправность. Ток ионизации вне допуска', 'Coms/CH_Start_Max_Heatout'),
    ('П-110-00', 'Неисправность. Нет протока воды', 'Coms/CH_Start_Min_Heatout'),
    ('П-110-01', 'Неисправность. Ток ионизации вне допуска', 'Coms/CH_Start_Min_Heatout'),
    ('П-111-00', 'DB_Gas_Safety_Time. Неисправность. Стенд не готов', 'Coms/Safety_Time'),
    ('П-111-01', 'DB_Gas_Safety_Time. Неисправность. Время закрытия клапана превышено', 'Coms/Safety_Time'),
    ('П-200-00', 'Нет протока воды', 'DHW/Fill_Circuit_Normal_Direction'),
    ('П-200-01', 'Нет давления воды', 'DHW/Fill_Circuit_Normal_Direction'),
    ('П-200-02', 'Время заполнения превышено', 'DHW/Fill_Circuit_Normal_Direction'),
    ('П-201-00', 'Неисправность. Низкий расход воды в контуре', 'DHW/Check_Tank_Mode'),
    ('П-201-01', 'Неисправность. Высокий расход воды в контуре', 'DHW/Check_Tank_Mode'),
    ('П-201-02', 'Неисправность. Нет давления воды', 'DHW/Check_Tank_Mode'),
    ('П-202-00', 'Неисправность. Система не готова к продувке', 'DHW/Purge_Circuit_Normal_Direction'),
    ('П-203-00', 'Неисправность. Система не готова к продувке', 'DHW/Purge_Circuit_Reverse_Direction'),
    ('П-204-00', 'Неисправность. Время заполнение превышено', 'DHW/Reduce_Circuit_Pressure'),
    ('П-205-00', 'Неисправность. Разность температур вне допуска', 'DHW/Check_Flow_Temperature_Rise'),
    ('П-205-01', 'Неисправность. Не разжёгся котёл', 'DHW/Check_Flow_Temperature_Rise'),
    ('П-205-02', 'Неисправность. Заданный расход воды не достигнут', 'DHW/Check_Flow_Temperature_Rise'),
    ('П-206-00', 'Неисправность. Стенд не готов', 'DHW/Get_Flow_NTC_Cold'),
    ('П-207-00', 'Неисправность. Стенд не готов', 'DHW/Check_Flow_Rate'),
    ('П-207-01', 'Неисправность. Давление не достигнуто', 'DHW/Check_Flow_Rate'),
    ('П-207-02', 'Неисправность. Слишком малый расход воды', 'DHW/Check_Flow_Rate'),
    ('П-207-03', 'Неисправность. Слишком большой расход воды', 'DHW/Check_Flow_Rate'),
    ('П-208-00', 'Неисправность. Стенд не готов к тесту', 'DHW/Compare_Flow_NTC_Temperature_Hot'),
    ('П-208-01', 'Неисправность. Разность температур вне допуска', 'DHW/Compare_Flow_NTC_Temperature_Hot'),
    ('П-209-00', 'Неисправность. Расход воды в контуре CH выше допустимого', 'DHW/Check_Water_Flow_When_In_DHW_Mode'),
    ('П-210-00', 'DB_DHW_High_Pressure_Test. Неисправность. Давление не достигнуто', 'DHW/High_Pressure_Test'),
    ('П-210-01', 'DB_DHW_High_Pressure_Test. Неисправность. Давление выше заданного', 'DHW/High_Pressure_Test'),
    ('П-211-00', 'DB_DHW_Set_Circuit_Pressure. Неисправность. Ошибка переключения 3-х ходового клапана', 'DHW/Set_Circuit_Pressure'),
    ('П-211-01', 'DB_DHW_Set_Circuit_Pressure. Неисправность. Низкий расход воды в контуре', 'DHW/Set_Circuit_Pressure'),
    ('П-211-02', 'DB_DHW_Set_Circuit_Pressure. Неисправность. Высокий расход воды в контуре', 'DHW/Set_Circuit_Pressure'),
    ('П-211-03', 'DB_DHW_Set_Circuit_Pressure. Неисправность. Давление не достигнуто', 'DHW/Set_Circuit_Pressure'),
    ('П-211-04', 'DB_DHW_Set_Circuit_Pressure. Неисправность. Давление выше заданного', 'DHW/Set_Circuit_Pressure'),
    ('П-212-00', 'DB_DHW_Compare_Flow_NTC_Temp_Cold. Неисправность. Стенд не готов', 'DHW/Compare_Flow_NTC_Temperature_Cold'),
    ('П-212-01', 'DB_DHW_Compare_Flow_NTC_Temp_Cold. Неисправность. Разность температур вне допуска', 'DHW/Compare_Flow_NTC_Temperature_Cold'),
    ('П-213-00', 'DB_Set_Tank_Mode. Неисправность. Низкий расход воды в контуре', 'DHW/Set_Tank_Mode'),
    ('П-213-01', 'DB_Set_Tank_Mode. Неисправность. Давление не достигнуто', 'DHW/Set_Tank_Mode'),
    ('П-300-00', 'Нет протока воды', 'CH/Fast_Fill_Circuit'),
    ('П-300-01', 'Нет давления воды', 'CH/Fast_Fill_Circuit'),
    ('П-300-02', 'Время заполнения превышено', 'CH/Fast_Fill_Circuit'),
    ('П-301-00', 'Нет протока воды', 'CH/Slow_Fill_Circuit'),
    ('П-301-01', 'Нет давления воды', 'CH/Slow_Fill_Circuit'),
    ('П-301-02', 'Время заполнения превышено', 'CH/Slow_Fill_Circuit'),
    ('П-302-00', 'Неисправность. Нет протока воды', 'CH/Check_Water_Flow'),
    ('П-302-01', 'Неисправность. Слишком малый расход воды', 'CH/Check_Water_Flow'),
    ('П-302-02', 'Неисправность. Слишком большой расход воды', 'CH/Check_Water_Flow'),
    ('П-302-03', 'Неисправность. Низкое давление воды', 'CH/Check_Water_Flow'),
    ('П-302-04', 'Неисправность. Высокое давление воды', 'CH/Check_Water_Flow'),
    ('П-303-00', 'Неисправность. Слишком малый расход воды', 'CH/Get_CHW_Flow_NTC_Cold'),
    ('П-303-01', 'Неисправность. Слишком большой расход воды', 'CH/Get_CHW_Flow_NTC_Cold'),
    ('П-303-02', 'Неисправность. Низкое давление воды', 'CH/Get_CHW_Flow_NTC_Cold'),
    ('П-303-03', 'Неисправность. Высокое давление воды', 'CH/Get_CHW_Flow_NTC_Cold'),
    ('П-304-00', 'Неисправность. Разность температур вне допуска', 'CH/Compare_Flow_NTC_Temperature_Cold'),
    ('П-304-01', 'Неисправность. Слишком малый расход воды', 'CH/Compare_Flow_NTC_Temperature_Cold'),
    ('П-304-02', 'Неисправность. Слишком большой расход воды', 'CH/Compare_Flow_NTC_Temperature_Cold'),
    ('П-304-03', 'Неисправность. Низкое давление воды', 'CH/Compare_Flow_NTC_Temperature_Cold'),
    ('П-304-04', 'Неисправность. Высокое давление воды', 'CH/Compare_Flow_NTC_Temperature_Cold'),
    ('П-305-00', 'Неисправность. Стенд не готов', 'CH/Compare_Flow_NTC_Temperatures_Hot'),
    ('П-305-01', 'Неисправность. Расход газа вне допуска', 'CH/Compare_Flow_NTC_Temperatures_Hot'),
    ('П-305-02', 'Неисправность. Давление газа вне допуска', 'CH/Compare_Flow_NTC_Temperatures_Hot'),
    ('П-305-03', 'Неисправность. Давление на горелке вне допуска', 'CH/Compare_Flow_NTC_Temperatures_Hot'),
    ('П-305-04', 'Неисправность. Слишком малый расход воды', 'CH/Compare_Flow_NTC_Temperatures_Hot'),
    ('П-305-05', 'Неисправность. Слишком большой расход воды', 'CH/Compare_Flow_NTC_Temperatures_Hot'),
    ('П-305-06', 'Неисправность. Низкое давление воды', 'CH/Compare_Flow_NTC_Temperatures_Hot'),
    ('П-305-07', 'Неисправность. Высокое давление воды', 'CH/Compare_Flow_NTC_Temperatures_Hot'),
    ('П-305-08', 'Неисправность. Время заполнение превышено', 'CH/Compare_Flow_NTC_Temperatures_Hot'),
    ('П-305-09', 'Неисправность. Разность температур вне допуска', 'CH/Compare_Flow_NTC_Temperatures_Hot'),
    ('П-306-00', 'Неисправность. Система не готова к продувке', 'CH/Purge_Circuit_Reverse_Direction'),
    ('П-307-00', 'Неисправность. Стенд не готов', 'CH/Check_Flow_Temperature_Rise'),
    ('П-307-01', 'Неисправность. Охлаждение не выкл.', 'CH/Check_Flow_Temperature_Rise'),
    ('П-307-02', 'Неисправность. Расход газа вне допуска', 'CH/Check_Flow_Temperature_Rise'),
    ('П-307-03', 'Неисправность. Давление газа вне допуска', 'CH/Check_Flow_Temperature_Rise'),
    ('П-307-04', 'Неисправность. Давление на горелке вне допуска', 'CH/Check_Flow_Temperature_Rise'),
    ('П-307-05', 'Неисправность. Слишком малый расход воды', 'CH/Check_Flow_Temperature_Rise'),
    ('П-307-06', 'Неисправность. Слишком большой расход воды', 'CH/Check_Flow_Temperature_Rise'),
    ('П-307-07', 'Неисправность. Низкое давление воды в контуре отопления', 'CH/Check_Flow_Temperature_Rise'),
    ('П-307-08', 'Неисправность. Высокое давление воды в контуре отопления', 'CH/Check_Flow_Temperature_Rise'),
    ('П-307-09', 'Неисправность. Время заполнение превышено', 'CH/Check_Flow_Temperature_Rise'),
    ('П-307-10', 'Неисправность. Изменение температуры вне заданных пределов', 'CH/Check_Flow_Temperature_Rise'),
    ('П-308-00', 'DB_CH_Close_Circuit_Valve. Неисправность. Насос котла работает', 'CH/Close_Circuit_Valve'),
    ('П-309-00', 'DB_CH_Purge_Circuit_Normal_Direction. Неисправность. Система не готова к продувке', 'CH/Purge_Circuit_Normal_Direction'),
    ('П-400-00', 'Утечка газа', 'Gas/Leak_Test'),
    ('П-400-01', 'Нет давления газа', 'Gas/Leak_Test'),
    ('П-401-00', 'Неисправность. Низкий расход газа', 'Gas/Wait_for_Gas_Flow'),
    ('П-401-01', 'Неисправность. Высокий расход газа', 'Gas/Wait_for_Gas_Flow'),
    ('П-401-02', 'Неисправность. Стенд не готов', 'Gas/Wait_for_Gas_Flow'),
    ('П-402-00', 'Неисправность. Низкий расход газа', 'Gas/Set_Required_Pressure'),
    ('П-402-01', 'Неисправность. Высокий расход газа', 'Gas/Set_Required_Pressure'),
    ('П-402-02', 'Неисправность. Стенд не готов', 'Gas/Set_Required_Pressure'),
    ('П-402-03', 'Неисправность. Заданное значение давления газа не достигнуто', 'Gas/Set_Required_Pressure'),
    ('П-403-00', 'Неисправность. Низкий расход газа', 'Gas/Set_Gas_and_P_Burner_Max_Levels'),
    ('П-403-01', 'Неисправность. Высокий расход газа', 'Gas/Set_Gas_and_P_Burner_Max_Levels'),
    ('П-403-02', 'Неисправность. Стенд не готов', 'Gas/Set_Gas_and_P_Burner_Max_Levels'),
    ('П-403-03', 'Неисправность. Не подключена трубка газового клапана', 'Gas/Set_Gas_and_P_Burner_Max_Levels'),
    ('П-404-00', 'Неисправность. Расход газа вне допуска', 'Gas/Check_Gas_and_P_Burner_Max_Levels'),
    ('П-404-01', 'Неисправность. Давление газа вне допуска', 'Gas/Check_Gas_and_P_Burner_Max_Levels'),
    ('П-404-02', 'Неисправность. Давление на горелке вне допуска', 'Gas/Check_Gas_and_P_Burner_Max_Levels'),
    ('П-404-03', 'Неисправность. Стенд не готов', 'Gas/Check_Gas_and_P_Burner_Max_Levels'),
    ('П-404-04', 'Неисправность. Не подключена трубка газового клапана', 'Gas/Check_Gas_and_P_Burner_Max_Levels'),
    ('П-405-00', 'Неисправность. Расход газа вне допуска', 'Gas/Check_Gas_and_P_Burner_Min_Levels'),
    ('П-405-01', 'Неисправность. Давление газа вне допуска', 'Gas/Check_Gas_and_P_Burner_Min_Levels'),
    ('П-405-02', 'Неисправность. Давление на горелке вне допуска', 'Gas/Check_Gas_and_P_Burner_Min_Levels'),
    ('П-405-03', 'Неисправность. Стенд не готов', 'Gas/Check_Gas_and_P_Burner_Min_Levels'),
    ('П-405-04', 'Неисправность. Не подключена трубка газового клапана', 'Gas/Check_Gas_and_P_Burner_Min_Levels'),
    ('П-406-00', 'Неисправность. Утечка газа', 'Gas/Close_Circuit'),
    ('П-407-00', 'Gas_Set_Gas_and_P_Burner_Min_Levels. Неисправность. Низкий расход газа', 'Gas/Set_Gas_and_P_Burner_Min_Levels'),
    ('П-407-01', 'Gas_Set_Gas_and_P_Burner_Min_Levels. Неисправность. Высокий расход газа', 'Gas/Set_Gas_and_P_Burner_Min_Levels'),
    ('П-407-02', 'Gas_Set_Gas_and_P_Burner_Min_Levels. Неисправность. Стенд не готов', 'Gas/Set_Gas_and_P_Burner_Min_Levels'),
    ('П-407-03', 'Gas_Set_Gas_and_P_Burner_Min_Levels. Неисправность. Не подключена трубка газового клапана', 'Gas/Set_Gas_and_P_Burner_Min_Levels'),
    ('П-500-00', 'Котел не заблокирован', 'Block Boiler Adapter'),
    ('П-500-01', 'Реле 17K5 неисправно', 'Block Boiler Adapter'),
    ('П-501-00', 'Не подключен силовой кабель', 'Elec/Connect_Power_Cable'),
    ('П-502-00', 'Клипса заземление не подключена', 'Elec/Connect_Earth_Clip'),
    ('ЭБУ-11', 'Не задана ступень вентилятора', NULL),
    ('ЭБУ-A7', 'Неисправность датчика температуры ГВС. Проверить подключение DHW NTC', NULL),
    ('ЭБУ-A8', 'Неисправен датчик наружной температуры. Проверить подключение датчика', NULL),
    ('ЭБУ-A9', 'Невозможность нагрева бойлера косвенного нагрева из режима защиты от замерзания', NULL),
    ('ЭБУ-Ad', 'Неисправность датчика температуры бойлера. Проверить подключение датчика', NULL),
    ('ЭБУ-C1', 'Вентилятор не может достичь заданных оборотов. Проверить подключение вентилятора', NULL),
    ('ЭБУ-C4', 'Пневматический выключатель закрыт до начала нагрева. Проверить подключение APS', NULL),
    ('ЭБУ-C6', 'Пневматический выключатель не закрывается. Проверить подключение APS', NULL),
    ('ЭБУ-C7', 'Не обнаружен тахосигнал с вентилятора. Проверить подключение вентилятора', NULL),
    ('ЭБУ-CA', 'Высокое давление воды в системе', NULL),
    ('ЭБУ-CE', 'Низкое давление воды в системе', NULL),
    ('ЭБУ-D7', 'Неисправность модулирующей катушки. Проверить подключение', NULL),
    ('ЭБУ-E2', 'Неисправность датчика температуры подающей линии. Проверить подключение CH NTC', NULL),
    ('ЭБУ-E9', 'Блокировка при перегреве', NULL),
    ('ЭБУ-E9-STB', 'Проверить подключение STB', NULL),
    ('ЭБУ-EA', 'Блокировка зажигания. Проверить электрод розжига', NULL),
    ('ЭБУ-EL', 'Потеря пламени: котёл не восстановил пламя за 7 секунд. Проверить электрод розжига или газовый клапан', NULL),
    ('ЭБУ-F7', 'Неисправность катушек клапанов. Проверить катушки клапанов', NULL),
    ('ЭБУ-FA', 'Неисправность клапанов регулятора давления газа. Проверить клапаны', NULL),
    ('ЭБУ-FD', 'Залипание кнопок', NULL),
    ('ЭБУ-FL', 'Неисправность датчика контроля пламени. Проверить датчик пламени', NULL),
    ('ЭБУ-IE', 'Внутренняя ошибка ЭБУ', NULL),
    ('ЭБУ-LA', 'Не достигается температура для проведения термической дезинфекции', NULL),
    ('ЭБУ-P', 'Не задан тип котла', NULL),
    ('ЭБУ-PA', 'Ошибка работы насоса (блокировка ротора). Проверить насос', NULL),
    ('ЭБУ-Pd', 'Ошибка работы насоса (нет жидкости). Проверить насос', NULL),
    ('ЭБУ-PE', 'Ошибка работы насоса (электропитание). Проверить электропитание насоса', NULL);

    IF EXISTS (
        SELECT 1 FROM src_errors GROUP BY code HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'Найдены дубли code в src_errors';
    END IF;

    SELECT string_agg(format('%s -> %s', s.code, s.related_step_name), E'\n' ORDER BY s.code)
    INTO v_missing_steps
    FROM src_errors s
    LEFT JOIN tb_step_final_test st
           ON lower(btrim(st.name)) = lower(btrim(s.related_step_name))
    WHERE s.related_step_name IS NOT NULL
      AND st.id IS NULL;

    IF v_missing_steps IS NOT NULL THEN
        RAISE EXCEPTION 'Не найдены шаги tb_step_final_test для кодов:%', E'\n' || v_missing_steps;
    END IF;

    -- Защита от неоднозначного case-insensitive сопоставления шага.
    SELECT string_agg(format('%s (%s)', q.related_step_name, q.cnt), E'\n' ORDER BY q.related_step_name)
    INTO v_ambiguous_steps
    FROM (
        SELECT s.related_step_name, COUNT(DISTINCT st.id) AS cnt
          FROM (
              SELECT DISTINCT related_step_name
                FROM src_errors
               WHERE related_step_name IS NOT NULL
          ) s
          JOIN tb_step_final_test st
            ON lower(btrim(st.name)) = lower(btrim(s.related_step_name))
         GROUP BY s.related_step_name
        HAVING COUNT(DISTINCT st.id) > 1
    ) q;

    IF v_ambiguous_steps IS NOT NULL THEN
        RAISE EXCEPTION 'Неоднозначное сопоставление шагов (case-insensitive):%', E'\n' || v_ambiguous_steps;
    END IF;

    UPDATE tb_error_settings_history h
       SET is_active = false
     WHERE h.is_active = true
       AND h.station_type_id = v_station_type_id;

    DELETE FROM tb_error_settings_template
     WHERE station_type_id = v_station_type_id;

    WITH base AS (
        SELECT s.code,
               s.description,
               st.id AS step_id,
               ROW_NUMBER() OVER (ORDER BY s.code) AS rn
          FROM src_errors s
          LEFT JOIN tb_step_final_test st
            ON lower(btrim(st.name)) = lower(btrim(s.related_step_name))
    ), mapped AS (
        SELECT (SELECT COALESCE(MAX(id), 0) FROM tb_error_settings_template) + b.rn AS id,
               b.code,
               b.description,
               b.step_id
          FROM base b
    ), inserted AS (
        INSERT INTO tb_error_settings_template(id, address_error, description, version, station_type_id, step_id)
        SELECT m.id, m.code, m.description, v_version, v_station_type_id, m.step_id
          FROM mapped m
         ORDER BY m.code
        RETURNING id, address_error, description, step_id
    ), history_rows AS (
        SELECT (SELECT COALESCE(MAX(id), 0) FROM tb_error_settings_history) + ROW_NUMBER() OVER (ORDER BY i.id) AS history_id,
               i.id AS error_settings_id,
               i.step_id AS step_final_test_id,
               i.address_error,
               i.description
          FROM inserted i
    )
    INSERT INTO tb_error_settings_history(id, error_settings_id, step_final_test_id, address_error, description, version, is_active, station_type_id)
    SELECT h.history_id,
           h.error_settings_id,
           h.step_final_test_id,
           h.address_error,
           h.description,
           v_version,
           true,
           v_station_type_id
      FROM history_rows h;

    -- Синхронизация sequence для Jmix (поддержка обоих неймингов sequence).
    v_template_seq := COALESCE(
        to_regclass('public.seq_id_tb_errorsettingstemplate')::text,
        to_regclass('public.tb_error_settings_template_id_seq')::text
    );
    IF v_template_seq IS NOT NULL THEN
        PERFORM setval(
            v_template_seq,
            COALESCE((SELECT MAX(id) FROM tb_error_settings_template), 0) + 1,
            false
        );
    END IF;

    v_history_seq := COALESCE(
        to_regclass('public.seq_id_tb_errorsettingshistory')::text,
        to_regclass('public.tb_error_settings_history_id_seq')::text
    );
    IF v_history_seq IS NOT NULL THEN
        PERFORM setval(
            v_history_seq,
            COALESCE((SELECT MAX(id) FROM tb_error_settings_history), 0) + 1,
            false
        );
    END IF;

    IF (SELECT COUNT(*) FROM tb_error_settings_template t WHERE t.station_type_id = v_station_type_id) <>
       (SELECT COUNT(*) FROM src_errors) THEN
        RAISE EXCEPTION 'Количество вставленных template не совпадает с источником';
    END IF;

    IF EXISTS (
        SELECT 1
          FROM tb_error_settings_template t
          LEFT JOIN tb_error_settings_history h
                 ON h.error_settings_id = t.id
                AND h.is_active = true
         WHERE t.station_type_id = v_station_type_id
           AND h.id IS NULL
    ) THEN
        RAISE EXCEPTION 'Найдены template без active history';
    END IF;
END $$;

COMMIT;

-- Постпроверки (выполнять отдельно):
-- SELECT COUNT(*) FROM tb_error_settings_template WHERE station_type_id = 7;
-- SELECT COUNT(*) FROM tb_error_settings_history WHERE station_type_id = 7 AND is_active = true;
-- SELECT address_error, COUNT(*) FROM tb_error_settings_template WHERE station_type_id = 7 GROUP BY address_error HAVING COUNT(*) > 1;
