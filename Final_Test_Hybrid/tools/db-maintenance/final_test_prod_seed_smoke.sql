\set ON_ERROR_STOP on

\echo Smoke-проверка первых insert после RESTART IDENTITY...

BEGIN;

DO $$
DECLARE
    v_boiler_type_id bigint;
    v_cycle_id bigint;
    v_boiler_id bigint;
    v_operation_id bigint;
BEGIN
    INSERT INTO "TB_BOILER_TYPE" ("ARTICLE", "TYPE")
    VALUES ('9999999999', 'Smoke Verify Type')
    RETURNING "ID" INTO v_boiler_type_id;

    IF v_boiler_type_id <> 1 THEN
        RAISE EXCEPTION 'Первый ID в TB_BOILER_TYPE ожидается 1, получено %', v_boiler_type_id;
    END IF;

    INSERT INTO "TB_BOILER_TYPE_CYCLE" ("BOILER_TYPE_ID", "TYPE", "IS_ACTIVE", "ARTICLE")
    VALUES (v_boiler_type_id, 'Smoke Verify Cycle', true, '9999999999')
    RETURNING "ID" INTO v_cycle_id;

    IF v_cycle_id <> 1 THEN
        RAISE EXCEPTION 'Первый ID в TB_BOILER_TYPE_CYCLE ожидается 1, получено %', v_cycle_id;
    END IF;

    INSERT INTO "TB_BOILER" ("SERIAL_NUMBER", "BOILER_TYPE_CYCLE_ID", "DATE_CREATE", "DATE_UPDATE", "STATUS", "OPERATOR")
    VALUES ('VERIFY-BOILER-0001', v_cycle_id, NOW() AT TIME ZONE 'UTC', NULL, 'InWork', 'db-maintenance')
    RETURNING "ID" INTO v_boiler_id;

    IF v_boiler_id <> 1 THEN
        RAISE EXCEPTION 'Первый ID в TB_BOILER ожидается 1, получено %', v_boiler_id;
    END IF;

    INSERT INTO "TB_OPERATION" ("DATE_START", "DATE_END", "BOILER_ID", "STATUS", "NUMBER_SHIFT", "COMMENT_", "ADMIN_INTERRUPTED", "VERSION", "OPERATOR")
    VALUES (NOW() AT TIME ZONE 'UTC', NULL, v_boiler_id, 'InWork', 1, NULL, NULL, 1, 'db-maintenance')
    RETURNING "ID" INTO v_operation_id;

    IF v_operation_id <> 1 THEN
        RAISE EXCEPTION 'Первый ID в TB_OPERATION ожидается 1, получено %', v_operation_id;
    END IF;
END $$;

ROLLBACK;

