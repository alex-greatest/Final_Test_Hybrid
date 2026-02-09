-- clear_error_templates_and_deactivate_history.sql
-- Цель: деактивировать активные записи history и удалить текущие шаблоны ошибок.
-- Важно: таблица tb_error не изменяется.

BEGIN;

UPDATE tb_error_settings_history
SET is_active = false
WHERE is_active = true;

DELETE FROM tb_error_settings_template;

COMMIT;

-- Постпроверки (выполнять отдельно):
-- SELECT COUNT(*) AS template_count FROM tb_error_settings_template;
-- SELECT is_active, COUNT(*) FROM tb_error_settings_history GROUP BY is_active ORDER BY is_active DESC;
-- SELECT COUNT(*) AS tb_error_count FROM tb_error;
