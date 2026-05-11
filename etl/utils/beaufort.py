"""
Beaufort Scale Conversion — WMO 2023 Standard

Nguồn: World Meteorological Organization
URL: https://www.wmo.int/pages/prog/www/DPFS/documents/Beaufort_Scale.pdf

Test cases (phải nhất quán với ASP.NET BeaufortConverter.ToBeaufort()):
  0.0 m/s  → Beaufort 0  (Calm)
  10.7 m/s → Beaufort 5  (Fresh Breeze — last LOW)
  10.8 m/s → Beaufort 6  (Strong Breeze — first MEDIUM)
  17.1 m/s → Beaufort 7  (High Wind — last MEDIUM)
  17.2 m/s → Beaufort 8  (Gale — first HIGH)
  24.4 m/s → Beaufort 9  (Strong Gale — last HIGH)
  24.5 m/s → Beaufort 10 (Storm — first CRITICAL)
  32.7 m/s → Beaufort 12 (Hurricane Force)
"""

from typing import Tuple

# WMO Beaufort Scale: (beaufort_number, min_ms_inclusive, max_ms_exclusive)
BEAUFORT_TABLE: list[Tuple[int, float, float | None]] = [
    (0,  0.0,   0.3),
    (1,  0.3,   1.6),
    (2,  1.6,   3.4),
    (3,  3.4,   5.5),
    (4,  5.5,   8.0),
    (5,  8.0,   10.8),
    (6,  10.8,  13.9),
    (7,  13.9,  17.2),
    (8,  17.2,  20.8),
    (9,  20.8,  24.5),
    (10, 24.5,  28.5),
    (11, 28.5,  32.7),
    (12, 32.7,  None),
]

BEAUFORT_TO_RISK: dict[int, str] = {
    0: "LOW", 1: "LOW", 2: "LOW", 3: "LOW", 4: "LOW", 5: "LOW",
    6: "MEDIUM", 7: "MEDIUM",
    8: "HIGH", 9: "HIGH",
    10: "CRITICAL", 11: "CRITICAL", 12: "CRITICAL",
}

RISK_LEVEL_INT: dict[str, int] = {
    "LOW": 1, "MEDIUM": 2, "HIGH": 3, "CRITICAL": 4
}
INT_TO_RISK_LEVEL: dict[int, str] = {v: k for k, v in RISK_LEVEL_INT.items()}


def ms_to_beaufort(wind_speed_ms: float) -> int:
    """
    Chuyển đổi tốc độ gió (m/s) sang cấp Beaufort theo WMO 2023.

    Examples:
        >>> ms_to_beaufort(0.0)
        0
        >>> ms_to_beaufort(10.7)
        5
        >>> ms_to_beaufort(10.8)
        6
        >>> ms_to_beaufort(100)
        12
    """
    if wind_speed_ms < 0:
        raise ValueError(f"wind_speed_ms must be non-negative, got {wind_speed_ms}")

    for beaufort, min_ms, max_ms in BEAUFORT_TABLE:
        if max_ms is None:
            return beaufort
        if min_ms <= wind_speed_ms < max_ms:
            return beaufort

    return 12


def beaufort_to_risk_level(beaufort: int) -> str:
    """Chuyển cấp Beaufort (0–12) sang risk level string."""
    if beaufort < 0 or beaufort > 12:
        raise ValueError(f"beaufort must be 0–12, got {beaufort}")
    return BEAUFORT_TO_RISK[beaufort]


def wind_to_risk(beaufort: int) -> int:
    """Trả về risk level dưới dạng int cho MAX aggregation. LOW=1..CRITICAL=4."""
    return RISK_LEVEL_INT[beaufort_to_risk_level(beaufort)]
