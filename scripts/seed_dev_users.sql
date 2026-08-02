-- Tài khoản chỉ dùng cho môi trường development.
-- Script có tính idempotent: chỉ tạo tài khoản còn thiếu, không ghi đè mật khẩu đã đổi.

\if :{?RESET_DEMO_PASSWORDS}
\else
\set RESET_DEMO_PASSWORDS false
\endif

BEGIN;

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
    crypt('Admin@2026!', gen_salt('bf', 12)),
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
    crypt('Admin@2026!', gen_salt('bf', 12)),
    account.role::operational.user_role_enum,
    'ACTIVE',
    port.id,
    NOW()
FROM (
    VALUES
        ('manager@porms.vn', 'Trần Thị Lan', 'PORT_MANAGER'),
        ('operator@porms.vn', 'Phạm Minh Đức', 'OPERATOR')
) AS account(email, full_name, role)
JOIN operational.ports port ON port.code = 'DNTSA' AND port.deleted_at IS NULL
ON CONFLICT DO NOTHING;

-- Chỉ đặt lại mật khẩu khi người chạy chủ động dùng -ResetDemoPasswords.
\if :RESET_DEMO_PASSWORDS
UPDATE operational.users
SET password_hash = crypt('Admin@2026!', gen_salt('bf', 12)),
    failed_login_count = 0,
    locked_until = NULL,
    password_changed_at = NOW(),
    updated_at = NOW()
WHERE LOWER(email) IN (
    'admin@porms.vn',
    'manager@porms.vn',
    'operator@porms.vn'
)
  AND deleted_at IS NULL;
\endif

COMMIT;
