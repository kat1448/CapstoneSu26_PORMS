from datetime import datetime, timedelta, timezone
import unittest

from models.weather_risk_ml import _recommendation, analyze_forecast_risk


class WeatherRiskMlModelTests(unittest.TestCase):
    def test_analyze_forecast_risk_clusters_stable_wind_and_severe_days(self):
        base_time = datetime(2026, 7, 10, tzinfo=timezone.utc)

        result = analyze_forecast_risk(
            port_code="DNTSA",
            items=[
                {
                    "planned_at": base_time,
                    "wind_speed_ms": 3.5,
                    "rainfall_mm": 0.0,
                    "visibility_km": 10.0,
                    "humidity_pct": 72,
                    "pressure_hpa": 1012,
                    "temperature_c": 29,
                    "rule_risk_level": "LOW",
                    "operation_plan": "NORMAL",
                },
                {
                    "planned_at": base_time + timedelta(days=1),
                    "wind_speed_ms": 16.0,
                    "rainfall_mm": 1.2,
                    "visibility_km": 7.0,
                    "humidity_pct": 81,
                    "pressure_hpa": 1006,
                    "temperature_c": 28,
                    "rule_risk_level": "HIGH",
                    "operation_plan": "LIMITED",
                },
                {
                    "planned_at": base_time + timedelta(days=2),
                    "wind_speed_ms": 23.0,
                    "rainfall_mm": 35.0,
                    "visibility_km": 1.5,
                    "humidity_pct": 94,
                    "pressure_hpa": 998,
                    "temperature_c": 27,
                    "rule_risk_level": "CRITICAL",
                    "operation_plan": "STOP",
                },
            ],
        )

        labels = {item["cluster_label"] for item in result["items"]}

        self.assertEqual(result["model_version"], "pca-kmeans-v1")
        self.assertEqual(result["port_code"], "DNTSA")
        self.assertEqual(len(result["items"]), 3)
        self.assertIn("STABLE_WEATHER", labels)
        self.assertIn("WIND_RISK", labels)
        self.assertIn("SEVERE_OPERATION_RISK", labels)
        self.assertTrue(all(0 <= item["pca_risk_score"] <= 100 for item in result["items"]))
        self.assertTrue(all(item["dominant_factors"] for item in result["items"]))

    def test_medium_ai_score_recommends_normal_operations(self):
        self.assertEqual(_recommendation("STABLE_WEATHER", 45), "NORMAL")


if __name__ == "__main__":
    unittest.main()
