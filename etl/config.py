from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
        extra="ignore",
    )

    # PostgreSQL
    POSTGRES_DSN: str = "postgresql://porms_etl:EtlPass123!@localhost:5432/porms_db"

    # OpenWeather (env var name matches docker-compose)
    OPENWEATHER_API_KEY: str = ""
    OW_BASE_URL: str = "https://api.openweathermap.org/data/2.5"

    # ASP.NET internal trigger (env var names match docker-compose)
    BACKEND_TRIGGER_URL: str = "http://backend:5000/api/internal/trigger-risk-engine"
    INTERNAL_API_KEY: str = ""

    # Prefect
    PREFECT_API_URL: str = "http://prefect-server:4200/api"

    # Logging
    LOG_LEVEL: str = "INFO"

    # Aliases for backwards-compat with design doc variable names
    @property
    def OW_API_KEY(self) -> str:
        return self.OPENWEATHER_API_KEY

    @property
    def ASP_NET_TRIGGER_URL(self) -> str:
        return self.BACKEND_TRIGGER_URL

    @property
    def ASP_NET_INTERNAL_KEY(self) -> str:
        return self.INTERNAL_API_KEY


settings = Settings()
