-- ================================================================
-- Tao tai khoan demo cho moi truong deployment
-- ================================================================
-- Mat khau duoc truyen qua bien psql demo_user_password tu file .env
-- tren server. Script chi tao tai khoan con thieu va khong dat lai mat
-- khau cua tai khoan da ton tai.

BEGIN;

-- Bao loi som neu du lieu khoi tao khong co cang demo DNTSA.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM operational.ports
        WHERE code = 'DNTSA'
          AND deleted_at IS NULL
    ) THEN
        RAISE EXCEPTION 'Demo port DNTSA was not found.';
    END IF;
END
$$;

INSERT INTO operational.users (
    email,
    full_name,
    password_hash,
    role,
    status,
    assigned_port_id,
    password_changed_at
)
VALUES (
    'admin@porms.vn',
    'System Administrator',
    crypt(:'demo_user_password', gen_salt('bf', 12)),
    'ADMIN',
    'ACTIVE',
    NULL,
    NOW()
)
ON CONFLICT DO NOTHING;

INSERT INTO operational.users (
    email,
    full_name,
    password_hash,
    role,
    status,
    assigned_port_id,
    password_changed_at
)
SELECT
    account.email,
    account.full_name,
    crypt(:'demo_user_password', gen_salt('bf', 12)),
    account.role::operational.user_role_enum,
    'ACTIVE',
    port.id,
    NOW()
FROM (
    VALUES
        ('manager@porms.vn', 'Tran Thi Lan', 'PORT_MANAGER'),
        ('operator@porms.vn', 'Pham Minh Duc', 'OPERATOR')
) AS account(email, full_name, role)
JOIN operational.ports port
  ON port.code = 'DNTSA'
 AND port.deleted_at IS NULL
ON CONFLICT DO NOTHING;

COMMIT;

