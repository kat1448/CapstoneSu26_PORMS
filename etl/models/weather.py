from datetime import datetime
from typing import Optional
from pydantic import BaseModel
import json


class WeatherReadingModel(BaseModel):
    port_id: str

    wind_speed_ms: float
    beaufort_number: int
    wind_direction_deg: Optional[int] = None
    wind_gust_ms: Optional[float] = None

    rainfall_1h_mm: float = 0.0
    rainfall_3h_mm: Optional[float] = None

    temperature_c: Optional[float] = None
    humidity_pct: Optional[int] = None
    visibility_km: Optional[float] = None
    pressure_hpa: Optional[float] = None

    ow_weather_code: Optional[int] = None
    ow_weather_desc: Optional[str] = None

    observed_at: datetime
    raw_payload: Optional[dict] = None
    is_simulation: bool = False

    def to_db_dict(self) -> dict:
        return {
            "port_id": self.port_id,
            "wind_speed_ms": self.wind_speed_ms,
            "beaufort_number": self.beaufort_number,
            "wind_direction_deg": self.wind_direction_deg,
            "wind_gust_ms": self.wind_gust_ms,
            "rainfall_1h_mm": self.rainfall_1h_mm,
            "rainfall_3h_mm": self.rainfall_3h_mm,
            "temperature_c": self.temperature_c,
            "humidity_pct": self.humidity_pct,
            "visibility_km": self.visibility_km,
            "pressure_hpa": self.pressure_hpa,
            "ow_weather_code": self.ow_weather_code,
            "ow_weather_desc": self.ow_weather_desc,
            "observed_at": self.observed_at,
            "raw_payload": json.dumps(self.raw_payload) if self.raw_payload else None,
            "is_simulation": self.is_simulation,
        }
