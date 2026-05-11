# weather_transformer chạy như subflow bên trong dw_loader — không có schedule độc lập.
# Logic transform được đặt trong tasks/transformer.py
# File này giữ lại để tương thích với cấu trúc thư mục ban đầu.

from tasks.transformer import transform_weather_readings

__all__ = ["transform_weather_readings"]
