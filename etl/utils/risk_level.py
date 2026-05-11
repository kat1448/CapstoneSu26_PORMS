"""
Risk level calculation utilities — dùng cho transformer.
Ngưỡng hardcode ở đây để ETL không phụ thuộc vào DB.
"""

RAIN_THRESHOLDS = [
    (0.0,  10.0,  "LOW"),
    (10.0, 25.0,  "MEDIUM"),
    (25.0, 50.0,  "HIGH"),
    (50.0, None,  "CRITICAL"),
]

VISIBILITY_THRESHOLDS = [
    (10.0, None,  "LOW"),
    (5.0,  10.0,  "MEDIUM"),
    (1.0,  5.0,   "HIGH"),
    (0.0,  1.0,   "CRITICAL"),
]

RISK_INT = {"LOW": 1, "MEDIUM": 2, "HIGH": 3, "CRITICAL": 4}


def rain_to_risk(rainfall_mm_h: float) -> int:
    """Chuyển lượng mưa (mm/h) sang risk level int."""
    for min_val, max_val, level in RAIN_THRESHOLDS:
        if max_val is None or rainfall_mm_h < max_val:
            if rainfall_mm_h >= min_val:
                return RISK_INT[level]
    return RISK_INT["CRITICAL"]


def visibility_to_risk(visibility_km: float | None) -> int:
    """Chuyển tầm nhìn (km) sang risk level int. None → LOW."""
    if visibility_km is None:
        return RISK_INT["LOW"]
    for min_val, max_val, level in VISIBILITY_THRESHOLDS:
        if max_val is None:
            if visibility_km >= min_val:
                return RISK_INT[level]
        elif min_val <= visibility_km < max_val:
            return RISK_INT[level]
    return RISK_INT["CRITICAL"]
