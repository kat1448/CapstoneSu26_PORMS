from prefect.deployments import Deployment
from prefect.server.schemas.schedules import CronSchedule, IntervalSchedule
from datetime import timedelta

from flows.weather_collector import weather_collector_flow
from flows.historical_backfill import historical_backfill_flow
from flows.forecast_plan_refresher import forecast_plan_refresh_flow


def deploy_all():
    """Tạo tất cả deployments vào Prefect Server."""

    # Weather Collector: mỗi 10 phút
    collector_deployment = Deployment.build_from_flow(
        flow=weather_collector_flow,
        name="prod",
        version="1.0.0",
        schedule=IntervalSchedule(interval=timedelta(minutes=10)),
        work_pool_name="porms-pool",
        tags=["production", "weather", "real-time"],
        description="Thu thập thời tiết mỗi 10 phút cho tất cả cảng active",
        parameters={},
    )
    collector_deployment.apply()
    print("✅ weather-collector deployed (every 10 min)")

    # Historical Backfill: thủ công
    backfill_deployment = Deployment.build_from_flow(
        flow=historical_backfill_flow,
        name="prod",
        version="1.0.0",
        schedule=None,
        work_pool_name="porms-pool",
        tags=["one-time", "backfill"],
        description="[ONE-TIME] Nạp dữ liệu lịch sử 30 ngày trước demo",
        parameters={"days_back": 30},
    )
    backfill_deployment.apply()
    print("✅ historical-backfill deployed (manual trigger only)")

    forecast_deployment = Deployment.build_from_flow(
        flow=forecast_plan_refresh_flow,
        name="prod",
        version="1.0.0",
        schedule=CronSchedule(cron="15 0 * * *", timezone="Asia/Ho_Chi_Minh"),
        work_pool_name="porms-pool",
        tags=["production", "forecast", "daily"],
        description="Làm mới kế hoạch dự báo 5 ngày cho tất cả cảng mỗi ngày",
        parameters={},
    )
    forecast_deployment.apply()
    print("✅ forecast-plan-refresh deployed (daily at 00:15)")

    print("\n🎉 All deployments registered!")
    print("📊 Prefect UI: http://localhost:4200")


if __name__ == "__main__":
    deploy_all()
