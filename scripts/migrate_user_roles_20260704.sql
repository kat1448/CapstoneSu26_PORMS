BEGIN;

ALTER TYPE operational.user_role_enum RENAME VALUE 'ADMIN' TO 'SUPER_ADMIN';
ALTER TYPE operational.user_role_enum RENAME VALUE 'PORT_MANAGER' TO 'ADMIN';
ALTER TYPE operational.user_role_enum RENAME VALUE 'OPERATOR' TO 'STANDARD_USER';

ALTER TABLE operational.users
    ALTER COLUMN role SET DEFAULT 'STANDARD_USER';

ALTER TABLE operational.users
    DROP CONSTRAINT IF EXISTS users_role_port_assignment;

ALTER TABLE operational.users
    ADD CONSTRAINT users_role_port_assignment CHECK (
        (role = 'SUPER_ADMIN' AND assigned_port_id IS NULL)
        OR
        (role IN ('ADMIN', 'STANDARD_USER') AND assigned_port_id IS NOT NULL)
    );

COMMENT ON TABLE operational.users
    IS 'Tài khoản người dùng — RBAC: SUPER_ADMIN/ADMIN/STANDARD_USER';

COMMENT ON COLUMN operational.users.assigned_port_id
    IS 'Port phụ trách. NULL = SUPER_ADMIN (xem tất cả cảng)';

COMMIT;
