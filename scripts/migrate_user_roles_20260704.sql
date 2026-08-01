BEGIN;

ALTER TABLE operational.users
    DROP CONSTRAINT IF EXISTS users_role_port_assignment;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_enum e
        JOIN pg_type t ON t.oid = e.enumtypid
        JOIN pg_namespace n ON n.oid = t.typnamespace
        WHERE n.nspname = 'operational'
          AND t.typname = 'user_role_enum'
          AND e.enumlabel = 'SUPER_ADMIN'
    ) THEN
        ALTER TYPE operational.user_role_enum RENAME VALUE 'ADMIN' TO 'PORT_MANAGER_TMP';
        ALTER TYPE operational.user_role_enum RENAME VALUE 'SUPER_ADMIN' TO 'ADMIN';
        ALTER TYPE operational.user_role_enum RENAME VALUE 'STANDARD_USER' TO 'OPERATOR';
        ALTER TYPE operational.user_role_enum RENAME VALUE 'PORT_MANAGER_TMP' TO 'PORT_MANAGER';
    END IF;
END $$;

ALTER TABLE operational.users
    ALTER COLUMN role SET DEFAULT 'OPERATOR';

ALTER TABLE operational.users
    ADD CONSTRAINT users_role_port_assignment CHECK (
        (role = 'ADMIN' AND assigned_port_id IS NULL)
        OR
        (role IN ('PORT_MANAGER', 'OPERATOR') AND assigned_port_id IS NOT NULL)
    );

COMMENT ON TABLE operational.users
    IS 'Tai khoan nguoi dung - RBAC: ADMIN/PORT_MANAGER/OPERATOR';

COMMENT ON COLUMN operational.users.assigned_port_id
    IS 'Port phu trach. NULL = ADMIN (xem tat ca cang)';

COMMIT;
