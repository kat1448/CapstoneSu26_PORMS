import pandas as pd
from utils.beaufort import wind_to_risk
from utils.risk_level import rain_to_risk

RISK_MAP = {1: "LOW", 2: "MEDIUM", 3: "HIGH", 4: "CRITICAL"}
MINUTES_PER_READING = 15


def transform_weather_readings(df: pd.DataFrame) -> pd.DataFrame:
    """
    Aggregate weather readings theo giờ theo port.

    Input: port_id, wind_speed_ms, beaufort_number, rainfall_1h_mm,
           visibility_km, temperature_c, observed_at
    Output: 1 row per (port_id, time_key) với các aggregate columns
    """
    if df.empty:
        return pd.DataFrame()

    df["observed_at"] = pd.to_datetime(df["observed_at"], utc=True)
    df["time_key"] = df["observed_at"].dt.strftime("%Y%m%d%H").astype(int)

    df["risk_level_int"] = df.apply(
        lambda row: max(
            wind_to_risk(int(row["beaufort_number"])),
            rain_to_risk(float(row["rainfall_1h_mm"] or 0)),
        ),
        axis=1,
    )

    grouped = df.groupby(["port_id", "time_key"])

    agg = grouped.agg(
        reading_count=("wind_speed_ms", "count"),
        avg_wind_speed_ms=("wind_speed_ms", "mean"),
        max_wind_speed_ms=("wind_speed_ms", "max"),
        max_beaufort=("beaufort_number", "max"),
        avg_beaufort=("beaufort_number", "mean"),
        total_rainfall_mm=("rainfall_1h_mm", "sum"),
        avg_temperature_c=("temperature_c", "mean"),
        avg_visibility_km=("visibility_km", "mean"),
        min_visibility_km=("visibility_km", "min"),
    ).reset_index()

    for level, level_int in [("low", 1), ("medium", 2), ("high", 3), ("critical", 4)]:
        agg[f"minutes_at_{level}"] = (
            grouped["risk_level_int"]
            .apply(lambda x: (x == level_int).sum())
            .values
        ) * MINUTES_PER_READING

    last_readings = (
        df.sort_values("observed_at")
        .groupby(["port_id", "time_key"])["risk_level_int"]
        .last()
        .reset_index()
    )
    last_readings["final_risk_level"] = last_readings["risk_level_int"].map(RISK_MAP)

    agg = agg.merge(
        last_readings[["port_id", "time_key", "final_risk_level"]],
        on=["port_id", "time_key"],
        how="left",
    )

    float_cols = [
        "avg_wind_speed_ms", "max_wind_speed_ms", "avg_beaufort",
        "total_rainfall_mm", "avg_temperature_c", "avg_visibility_km", "min_visibility_km",
    ]
    for col in float_cols:
        if col in agg.columns:
            agg[col] = agg[col].round(2)

    return agg
