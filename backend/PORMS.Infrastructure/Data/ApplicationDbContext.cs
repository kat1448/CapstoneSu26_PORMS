using Microsoft.EntityFrameworkCore;
using PORMS.Application.Common.Interfaces;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.Infrastructure.Data;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Port> Ports => Set<Port>();
    public DbSet<WeatherReading> WeatherReadings => Set<WeatherReading>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<RiskThreshold> RiskThresholds => Set<RiskThreshold>();
    public DbSet<OperationEvent> OperationEvents => Set<OperationEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("operational");
        modelBuilder.HasPostgresEnum<RiskLevel>("operational", "risk_level_enum");
        modelBuilder.HasPostgresEnum<WeatherFactor>("operational", "weather_factor_enum");
        modelBuilder.HasPostgresEnum<OperationEventType>("operational", "event_type_enum");
        modelBuilder.HasPostgresEnum<OperationMode>("operational", "operation_mode_enum");

        modelBuilder.Entity<Port>(entity =>
        {
            entity.ToTable("ports");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Code).HasColumnName("code");
            entity.Property(x => x.Address).HasColumnName("address");
            entity.Property(x => x.Latitude).HasColumnName("latitude");
            entity.Property(x => x.Longitude).HasColumnName("longitude");
            entity.Property(x => x.Timezone).HasColumnName("timezone");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CurrentMode).HasColumnName("current_mode");
            entity.Property(x => x.CurrentRiskLevel).HasColumnName("current_risk_level");
            entity.Property(x => x.OpenWeatherStationId).HasColumnName("ow_station_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        });

        modelBuilder.Entity<WeatherReading>(entity =>
        {
            entity.ToTable("weather_readings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.PortId).HasColumnName("port_id");
            entity.Property(x => x.WindSpeedMs).HasColumnName("wind_speed_ms").HasPrecision(6, 2);
            entity.Property(x => x.BeaufortNumber).HasColumnName("beaufort_number");
            entity.Property(x => x.WindDirectionDeg).HasColumnName("wind_direction_deg");
            entity.Property(x => x.WindGustMs).HasColumnName("wind_gust_ms").HasPrecision(6, 2);
            entity.Property(x => x.Rainfall1hMm).HasColumnName("rainfall_1h_mm").HasPrecision(7, 2);
            entity.Property(x => x.Rainfall3hMm).HasColumnName("rainfall_3h_mm").HasPrecision(7, 2);
            entity.Property(x => x.TemperatureC).HasColumnName("temperature_c").HasPrecision(5, 2);
            entity.Property(x => x.HumidityPct).HasColumnName("humidity_pct");
            entity.Property(x => x.VisibilityKm).HasColumnName("visibility_km").HasPrecision(6, 2);
            entity.Property(x => x.PressureHpa).HasColumnName("pressure_hpa").HasPrecision(7, 2);
            entity.Property(x => x.OpenWeatherCode).HasColumnName("ow_weather_code");
            entity.Property(x => x.OpenWeatherDescription).HasColumnName("ow_weather_desc");
            entity.Ignore(x => x.OpenWeatherIcon);
            entity.Property(x => x.ObservedAt).HasColumnName("observed_at");
            entity.Property(x => x.RecordedAt).HasColumnName("recorded_at");
            entity.Property(x => x.DataSource).HasColumnName("data_source");
            entity.Property(x => x.RawPayload).HasColumnName("raw_payload").HasColumnType("jsonb");
            entity.Property(x => x.IsSimulation).HasColumnName("is_simulation");
            entity.HasOne(x => x.Port).WithMany().HasForeignKey(x => x.PortId);
        });

        modelBuilder.Entity<RiskAssessment>(entity =>
        {
            entity.ToTable("risk_assessments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.PortId).HasColumnName("port_id");
            entity.Property(x => x.WeatherReadingId).HasColumnName("weather_reading_id");
            entity.Property(x => x.FinalRiskLevel).HasColumnName("final_risk_level");
            entity.Property(x => x.WindRiskLevel).HasColumnName("wind_risk_level");
            entity.Property(x => x.RainRiskLevel).HasColumnName("rain_risk_level");
            entity.Property(x => x.VisibilityRiskLevel).HasColumnName("visibility_risk_level");
            entity.Property(x => x.BeaufortNumber).HasColumnName("beaufort_number");
            entity.Property(x => x.Rainfall1hMm).HasColumnName("rainfall_1h_mm").HasPrecision(7, 2);
            entity.Property(x => x.VisibilityKm).HasColumnName("visibility_km").HasPrecision(6, 2);
            entity.Property(x => x.PreviousRiskLevel).HasColumnName("previous_risk_level");
            entity.Property(x => x.LevelChanged).HasColumnName("level_changed");
            entity.Property(x => x.AssessmentSummary).HasColumnName("assessment_summary");
            entity.Property(x => x.EvaluatedAt).HasColumnName("evaluated_at");
            entity.Property(x => x.IsSimulation).HasColumnName("is_simulation");
            entity.HasOne(x => x.Port).WithMany().HasForeignKey(x => x.PortId);
            entity.HasOne(x => x.WeatherReading).WithMany().HasForeignKey(x => x.WeatherReadingId);
        });

        modelBuilder.Entity<RiskThreshold>(entity =>
        {
            entity.ToTable("risk_thresholds");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Factor).HasColumnName("factor");
            entity.Property(x => x.RiskLevel).HasColumnName("risk_level");
            entity.Property(x => x.MinValue).HasColumnName("min_value").HasPrecision(10, 3);
            entity.Property(x => x.MaxValue).HasColumnName("max_value").HasPrecision(10, 3);
            entity.Property(x => x.Unit).HasColumnName("unit");
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        });

        modelBuilder.Entity<OperationEvent>(entity =>
        {
            entity.ToTable("operation_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.PortId).HasColumnName("port_id");
            entity.Property(x => x.EventType).HasColumnName("event_type");
            entity.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.Property(x => x.Summary).HasColumnName("summary");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.IsSimulation).HasColumnName("is_simulation");
        });
    }
}
