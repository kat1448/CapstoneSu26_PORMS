import structlog
from enum import Enum

logger = structlog.get_logger()


class ErrorCategory(str, Enum):
    NETWORK = "NETWORK"
    DATA_QUALITY = "DATA_QUALITY"
    DATABASE = "DATABASE"
    BUSINESS = "BUSINESS"


def handle_fetch_error(port_code: str, error: Exception) -> dict:
    """Classify và log lỗi fetch. Trả về error context để ghi vào fetch_jobs."""
    import httpx

    if isinstance(error, httpx.TimeoutException):
        category = ErrorCategory.NETWORK
        message = f"OpenWeather API timeout after 10s for port {port_code}"
    elif isinstance(error, httpx.HTTPStatusError):
        category = ErrorCategory.NETWORK
        message = f"OpenWeather API HTTP {error.response.status_code}: {error.response.text[:200]}"
    elif isinstance(error, (KeyError, ValueError)):
        category = ErrorCategory.DATA_QUALITY
        message = f"Invalid API response format: {str(error)}"
    else:
        category = ErrorCategory.BUSINESS
        message = f"Unexpected error: {type(error).__name__}: {str(error)}"

    logger.error(
        "fetch_error",
        port_code=port_code,
        category=category,
        error_message=message,
    )

    return {"category": category, "message": message[:500]}
