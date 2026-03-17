\set ON_ERROR_STOP on

\echo Проверка пустоты прикладных таблиц и состояния __EFMigrationsHistory...

DO $$
DECLARE
    v_count bigint;
BEGIN
    SELECT COUNT(*) INTO v_count FROM "__EFMigrationsHistory";
    IF v_count = 0 THEN
        RAISE EXCEPTION 'Таблица __EFMigrationsHistory пуста';
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_BOILER_TYPE";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_BOILER_TYPE не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_BOILER_TYPE_CYCLE";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_BOILER_TYPE_CYCLE не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_RECIPE";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_RECIPE не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_RESULT_SETTINGS";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_RESULT_SETTINGS не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_RESULT_SETTING_HISTORY";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_RESULT_SETTING_HISTORY не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_STEP_FINAL_TEST";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_STEP_FINAL_TEST не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_STEP_FINAL_TEST_HISTORY";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_STEP_FINAL_TEST_HISTORY не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_ERROR_SETTINGS_TEMPLATE";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_ERROR_SETTINGS_TEMPLATE не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_ERROR_SETTINGS_HISTORY";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_ERROR_SETTINGS_HISTORY не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_BOILER";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_BOILER не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_OPERATION";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_OPERATION не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_RESULT";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_RESULT не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_ERROR";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_ERROR не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_STEP_TIME";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_STEP_TIME не пуста: %', v_count;
    END IF;

    SELECT COUNT(*) INTO v_count FROM "TB_SUCCESS_COUNT";
    IF v_count <> 0 THEN
        RAISE EXCEPTION 'TB_SUCCESS_COUNT не пуста: %', v_count;
    END IF;
END $$;

SELECT *
FROM (
    SELECT '__EFMigrationsHistory' AS table_name, COUNT(*)::bigint AS row_count FROM "__EFMigrationsHistory"
    UNION ALL SELECT 'TB_BOILER_TYPE', COUNT(*) FROM "TB_BOILER_TYPE"
    UNION ALL SELECT 'TB_BOILER_TYPE_CYCLE', COUNT(*) FROM "TB_BOILER_TYPE_CYCLE"
    UNION ALL SELECT 'TB_RECIPE', COUNT(*) FROM "TB_RECIPE"
    UNION ALL SELECT 'TB_RESULT_SETTINGS', COUNT(*) FROM "TB_RESULT_SETTINGS"
    UNION ALL SELECT 'TB_RESULT_SETTING_HISTORY', COUNT(*) FROM "TB_RESULT_SETTING_HISTORY"
    UNION ALL SELECT 'TB_STEP_FINAL_TEST', COUNT(*) FROM "TB_STEP_FINAL_TEST"
    UNION ALL SELECT 'TB_STEP_FINAL_TEST_HISTORY', COUNT(*) FROM "TB_STEP_FINAL_TEST_HISTORY"
    UNION ALL SELECT 'TB_ERROR_SETTINGS_TEMPLATE', COUNT(*) FROM "TB_ERROR_SETTINGS_TEMPLATE"
    UNION ALL SELECT 'TB_ERROR_SETTINGS_HISTORY', COUNT(*) FROM "TB_ERROR_SETTINGS_HISTORY"
    UNION ALL SELECT 'TB_BOILER', COUNT(*) FROM "TB_BOILER"
    UNION ALL SELECT 'TB_OPERATION', COUNT(*) FROM "TB_OPERATION"
    UNION ALL SELECT 'TB_RESULT', COUNT(*) FROM "TB_RESULT"
    UNION ALL SELECT 'TB_ERROR', COUNT(*) FROM "TB_ERROR"
    UNION ALL SELECT 'TB_STEP_TIME', COUNT(*) FROM "TB_STEP_TIME"
    UNION ALL SELECT 'TB_SUCCESS_COUNT', COUNT(*) FROM "TB_SUCCESS_COUNT"
) AS summary
ORDER BY table_name;

