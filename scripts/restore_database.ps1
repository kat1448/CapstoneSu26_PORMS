param(
    [string]$BackupFile = "docs/database/backups/PORMS_Demo_Data.backup",
    [string]$ContainerName = "porms-postgres",
    [string]$DatabaseName = "porms_db"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedBackup = Join-Path $repositoryRoot $BackupFile
$prepareRestoreFile = Join-Path $PSScriptRoot "prepare_database_restore.sql"

if (-not (Test-Path -LiteralPath $resolvedBackup)) {
    throw "Backup file not found: $resolvedBackup"
}
if (-not (Test-Path -LiteralPath $prepareRestoreFile)) {
    throw "Restore preparation script not found: $prepareRestoreFile"
}

$containerBackup = "/tmp/porms_demo_data.backup"
$containerPrepareRestore = "/tmp/porms_prepare_restore.sql"
docker cp $resolvedBackup "${ContainerName}:${containerBackup}"
if ($LASTEXITCODE -ne 0) {
    throw "Could not copy the backup into PostgreSQL container."
}
docker cp $prepareRestoreFile "${ContainerName}:${containerPrepareRestore}"
if ($LASTEXITCODE -ne 0) {
    throw "Could not copy the restore preparation script into PostgreSQL container."
}

docker exec $ContainerName psql `
    --username postgres `
    --dbname $DatabaseName `
    --file $containerPrepareRestore

if ($LASTEXITCODE -ne 0) {
    throw "Could not clear existing PORMS data before restore."
}

docker exec $ContainerName pg_restore `
    --username postgres `
    --dbname $DatabaseName `
    --data-only `
    --disable-triggers `
    --no-owner `
    --no-privileges `
    $containerBackup

if ($LASTEXITCODE -ne 0) {
    throw "Database restore failed."
}

docker exec $ContainerName psql `
    --username postgres `
    --dbname $DatabaseName `
    --command "TRUNCATE operational.refresh_tokens, operational.password_reset_tokens RESTART IDENTITY CASCADE;"

if ($LASTEXITCODE -ne 0) {
    throw "Could not clear authentication tokens after restore."
}

Write-Host "PORMS demo data restored successfully. Existing schema was kept and authentication tokens were cleared."
