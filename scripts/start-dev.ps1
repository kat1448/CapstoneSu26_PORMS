[CmdletBinding()]
param(
    [switch]$InfrastructureOnly,
    [switch]$ResetDemoPasswords
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repoRoot ".env"
$composeFile = Join-Path $repoRoot "infra\docker-compose.yml"
$roleMigrationFile = Join-Path $PSScriptRoot "migrate_user_roles_20260704.sql"
$devUserSeedFile = Join-Path $PSScriptRoot "seed_dev_users.sql"

function Invoke-PormsCompose {
    param([string[]]$ComposeArguments)

    & docker compose --env-file $envFile -f $composeFile @ComposeArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose thất bại: $($ComposeArguments -join ' ')"
    }
}

function Copy-SqlToPostgres {
    param(
        [string]$SourcePath,
        [string]$ContainerPath,
        [string]$ContainerId
    )

    & docker cp $SourcePath "${ContainerId}:$ContainerPath"
    if ($LASTEXITCODE -ne 0) {
        throw "Không thể sao chép SQL vào PostgreSQL container: $SourcePath"
    }
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Không tìm thấy Docker CLI. Hãy cài và khởi động Docker Desktop."
}

if (-not (Test-Path -LiteralPath $envFile)) {
    throw "Không tìm thấy file .env. Hãy sao chép .env.example thành .env và điền cấu hình."
}

Write-Host "[PORMS] Khởi động PostgreSQL..."
Invoke-PormsCompose -ComposeArguments @("up", "-d", "postgres")

# Đợi toàn bộ schema hoàn tất, không chỉ đợi PostgreSQL nhận kết nối.
$databaseReady = $false
$databaseReadyQuery = @"
SELECT to_regclass('operational.users') IS NOT NULL
   AND EXISTS (SELECT 1 FROM operational.ports WHERE code = 'DNTSA' AND deleted_at IS NULL)
   AND EXISTS (SELECT 1 FROM analytics.dim_time WHERE time_key = 2028123123);
"@

for ($attempt = 1; $attempt -le 60; $attempt++) {
    $databaseReadyResult = & docker compose --env-file $envFile -f $composeFile exec -T postgres `
        psql -U postgres -d porms_db -tA -c $databaseReadyQuery 2> $null

    if ($LASTEXITCODE -eq 0 -and ($databaseReadyResult | Out-String).Trim() -eq "t") {
        $databaseReady = $true
        break
    }

    Start-Sleep -Seconds 2
}

if (-not $databaseReady) {
    throw "Database chưa khởi tạo xong sau 120 giây. Hãy kiểm tra trạng thái Docker Compose."
}

$containerId = (& docker compose --env-file $envFile -f $composeFile ps -q postgres | Select-Object -First 1).Trim()
if (-not $containerId) {
    throw "Không tìm thấy PostgreSQL container đang chạy."
}

$legacyRoleQuery = @"
SELECT EXISTS (
    SELECT 1
    FROM pg_enum enum_value
    JOIN pg_type enum_type ON enum_type.oid = enum_value.enumtypid
    JOIN pg_namespace namespace ON namespace.oid = enum_type.typnamespace
    WHERE namespace.nspname = 'operational'
      AND enum_type.typname = 'user_role_enum'
      AND enum_value.enumlabel = 'SUPER_ADMIN'
);
"@

$legacyRoleResult = & docker compose --env-file $envFile -f $composeFile exec -T postgres `
    psql -U postgres -d porms_db -tA -c $legacyRoleQuery
if ($LASTEXITCODE -ne 0) {
    throw "Không thể kiểm tra phiên bản role trong database."
}

if (($legacyRoleResult | Out-String).Trim() -eq "t") {
    Write-Host "[PORMS] Phát hiện role cũ. Đang chạy migration role..."
    Copy-SqlToPostgres -SourcePath $roleMigrationFile `
        -ContainerPath "/tmp/porms_migrate_user_roles.sql" -ContainerId $containerId
    Invoke-PormsCompose -ComposeArguments @(
        "exec", "-T", "postgres", "psql", "-v", "ON_ERROR_STOP=1", "-U", "postgres",
        "-d", "porms_db", "-f", "/tmp/porms_migrate_user_roles.sql"
    )
}

Write-Host "[PORMS] Kiểm tra tài khoản development..."
Copy-SqlToPostgres -SourcePath $devUserSeedFile `
    -ContainerPath "/tmp/porms_seed_dev_users.sql" -ContainerId $containerId
$resetDemoPasswordsValue = if ($ResetDemoPasswords) { "true" } else { "false" }
Invoke-PormsCompose -ComposeArguments @(
    "exec", "-T", "postgres", "psql", "-v", "ON_ERROR_STOP=1",
    "-v", "RESET_DEMO_PASSWORDS=$resetDemoPasswordsValue", "-U", "postgres",
    "-d", "porms_db", "-f", "/tmp/porms_seed_dev_users.sql"
)

if ($ResetDemoPasswords) {
    Write-Warning "Mật khẩu của ba tài khoản demo đã được đặt lại thành Admin@2026!."
}

if ($InfrastructureOnly) {
    Write-Host "[PORMS] Khởi động các dịch vụ hạ tầng..."
    Invoke-PormsCompose -ComposeArguments @("up", "-d")
} else {
    Write-Host "[PORMS] Build và khởi động toàn bộ development stack..."
    Invoke-PormsCompose -ComposeArguments @("--profile", "app", "up", "-d", "--build")
}

Write-Host ""
Write-Host "PORMS development environment đã sẵn sàng."
Write-Host "Frontend: http://localhost:5173"
Write-Host "Swagger : http://localhost:5000/swagger"
Write-Host "Tài khoản demo dùng mật khẩu: Admin@2026!"
Write-Host "- admin@porms.vn (ADMIN)"
Write-Host "- manager@porms.vn (PORT_MANAGER)"
Write-Host "- operator@porms.vn (OPERATOR)"
