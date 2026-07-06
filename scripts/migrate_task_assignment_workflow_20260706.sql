ALTER TABLE operational.tasks
    ADD COLUMN IF NOT EXISTS alert_id UUID REFERENCES operational.alerts(id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS started_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS completion_note TEXT;

CREATE INDEX IF NOT EXISTS idx_tasks_alert
    ON operational.tasks (alert_id, created_at DESC)
    WHERE alert_id IS NOT NULL;
