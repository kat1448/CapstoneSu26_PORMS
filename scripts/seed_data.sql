-- PORMS reference ports for multi-port weather monitoring.
-- Idempotent: safe to run again after moving to another machine.
WITH port_seed(code, name, address, latitude, longitude) AS (
    VALUES
        ('VNCLI', 'Cảng Cát Lái', 'Phường Cát Lái, TP. Hồ Chí Minh', 10.766797, 106.795477),
        ('VNLCH', 'Cảng Lạch Huyện', 'Đặc khu Cát Hải, Hải Phòng', 20.798367, 106.905530),
        ('VNCMI', 'Cảng Quốc tế Cái Mép', 'Phường Tân Phước, TP. Hồ Chí Minh', 10.536950, 107.032258)
)
INSERT INTO operational.ports (
    code, name, address, latitude, longitude, timezone, weather_source,
    is_active, current_risk_level, current_operation_mode
)
SELECT code, name, address, latitude, longitude, 'Asia/Ho_Chi_Minh',
       'OPENWEATHER', TRUE, 'LOW', 'NORMAL'
FROM port_seed
ON CONFLICT ((UPPER(code))) WHERE deleted_at IS NULL DO UPDATE
SET name = EXCLUDED.name,
    address = EXCLUDED.address,
    latitude = EXCLUDED.latitude,
    longitude = EXCLUDED.longitude,
    weather_source = EXCLUDED.weather_source,
    is_active = TRUE,
    deleted_at = NULL;

WITH zone_seed(port_code, zone_name, zone_type, latitude, longitude, display_order) AS (
    VALUES
        ('VNCLI', 'Khu cầu bến', 'DOCK', 10.766500, 106.796000, 1),
        ('VNCLI', 'Bãi container', 'YARD', 10.767100, 106.794900, 2),
        ('VNCLI', 'Cổng ra vào', 'GATE', 10.768000, 106.794400, 3),
        ('VNLCH', 'Khu cầu bến', 'DOCK', 20.798100, 106.906200, 1),
        ('VNLCH', 'Bãi container', 'YARD', 20.799000, 106.904900, 2),
        ('VNLCH', 'Kho hàng', 'WAREHOUSE', 20.797700, 106.904600, 3),
        ('VNCMI', 'Khu cầu bến', 'DOCK', 10.536500, 107.032800, 1),
        ('VNCMI', 'Bãi container', 'YARD', 10.537400, 107.031900, 2),
        ('VNCMI', 'Cổng ra vào', 'GATE', 10.538000, 107.031400, 3)
)
INSERT INTO operational.zones (
    port_id, name, zone_type, latitude, longitude, display_order, is_active
)
SELECT p.id, z.zone_name, z.zone_type::operational.zone_type_enum,
       z.latitude, z.longitude, z.display_order, TRUE
FROM zone_seed z
JOIN operational.ports p ON p.code = z.port_code AND p.deleted_at IS NULL
ON CONFLICT (port_id, (LOWER(name))) WHERE deleted_at IS NULL DO UPDATE
SET zone_type = EXCLUDED.zone_type,
    latitude = EXCLUDED.latitude,
    longitude = EXCLUDED.longitude,
    display_order = EXCLUDED.display_order,
    is_active = TRUE,
    deleted_at = NULL;

-- Clean up legacy rows that were created when this UTF-8 file was piped
-- through a non-Unicode Windows console. The correctly encoded rows above
-- remain active, while the broken duplicates are hidden from every API query.
UPDATE operational.zones z
SET is_active = FALSE,
    deleted_at = COALESCE(z.deleted_at, NOW()),
    updated_at = NOW()
FROM operational.ports p
WHERE p.id = z.port_id
  AND p.code IN ('VNCLI', 'VNLCH', 'VNCMI')
  AND z.deleted_at IS NULL
  AND z.name IN (
      'Khu c?u b?n',
      'B?i container',
      'C?ng ra v?o',
      'Kho h?ng'
  );
