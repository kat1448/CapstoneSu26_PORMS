from pathlib import Path


def test_weather_collector_uses_current_weather_column_names() -> None:
    etl_root = Path(__file__).resolve().parents[1]
    collector = (etl_root / "flows/weather_collector.py").read_text(encoding="utf-8")
    model = (etl_root / "models/weather.py").read_text(encoding="utf-8")

    assert "weather_code" in collector
    assert "weather_description" in collector
    assert "ow_weather_code" not in collector
    assert "ow_weather_desc" not in collector

    assert "weather_code" in model
    assert "weather_description" in model
    assert "ow_weather_code" not in model
    assert "ow_weather_desc" not in model
