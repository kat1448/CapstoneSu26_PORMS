import pytest
from utils.beaufort import ms_to_beaufort, beaufort_to_risk_level, wind_to_risk


class TestMsToBeaufort:
    """Test cases verify với WMO Beaufort Scale 2023."""

    def test_calm(self):
        assert ms_to_beaufort(0.0) == 0
        assert ms_to_beaufort(0.2) == 0

    def test_light_air(self):
        assert ms_to_beaufort(0.3) == 1
        assert ms_to_beaufort(1.5) == 1

    def test_boundary_low_medium(self):
        """10.7 m/s là cấp 5 (LOW), 10.8 m/s là cấp 6 (MEDIUM)."""
        assert ms_to_beaufort(10.7) == 5
        assert ms_to_beaufort(10.8) == 6
        assert beaufort_to_risk_level(5) == "LOW"
        assert beaufort_to_risk_level(6) == "MEDIUM"

    def test_boundary_medium_high(self):
        """17.1 m/s là cấp 7 (MEDIUM), 17.2 m/s là cấp 8 (HIGH)."""
        assert ms_to_beaufort(17.1) == 7
        assert ms_to_beaufort(17.2) == 8
        assert beaufort_to_risk_level(7) == "MEDIUM"
        assert beaufort_to_risk_level(8) == "HIGH"

    def test_boundary_high_critical(self):
        """24.4 m/s là cấp 9 (HIGH), 24.5 m/s là cấp 10 (CRITICAL)."""
        assert ms_to_beaufort(24.4) == 9
        assert ms_to_beaufort(24.5) == 10
        assert beaufort_to_risk_level(9) == "HIGH"
        assert beaufort_to_risk_level(10) == "CRITICAL"

    def test_hurricane_force(self):
        """Beaufort 12: ≥ 32.7 m/s, không giới hạn trên."""
        assert ms_to_beaufort(32.7) == 12
        assert ms_to_beaufort(50.0) == 12
        assert ms_to_beaufort(100.0) == 12

    def test_negative_raises(self):
        with pytest.raises(ValueError):
            ms_to_beaufort(-1.0)

    @pytest.mark.parametrize("ms,expected_beaufort", [
        (0.0, 0), (1.0, 1), (5.0, 3), (17.0, 7),
        (25.0, 10), (32.6, 11), (32.7, 12),
    ])
    def test_parametrized(self, ms, expected_beaufort):
        assert ms_to_beaufort(ms) == expected_beaufort


class TestWindToRisk:
    def test_low_risk(self):
        assert wind_to_risk(0) == 1   # LOW
        assert wind_to_risk(5) == 1

    def test_medium_risk(self):
        assert wind_to_risk(6) == 2   # MEDIUM
        assert wind_to_risk(7) == 2

    def test_high_risk(self):
        assert wind_to_risk(8) == 3   # HIGH
        assert wind_to_risk(9) == 3

    def test_critical_risk(self):
        assert wind_to_risk(10) == 4  # CRITICAL
        assert wind_to_risk(12) == 4
