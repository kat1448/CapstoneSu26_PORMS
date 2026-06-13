using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;
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

    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<OperationModeLog> OperationModeLogs => Set<OperationModeLog>();
    public DbSet<OperationEvent> OperationEvents => Set<OperationEvent>();
    public DbSet<Port> Ports => Set<Port>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<RiskAssessmentDetail> RiskAssessmentDetails => Set<RiskAssessmentDetail>();
    public DbSet<RiskThreshold> RiskThresholds => Set<RiskThreshold>();
    public DbSet<SimulationSession> SimulationSessions => Set<SimulationSession>();
    public DbSet<SopExecution> SopExecutions => Set<SopExecution>();
    public DbSet<SopRule> SopRules => Set<SopRule>();
    public DbSet<TaskLog> TaskLogs => Set<TaskLog>();
    public DbSet<User> Users => Set<User>();
    public DbSet<WeatherReading> WeatherReadings => Set<WeatherReading>();
    public DbSet<WeatherFetchJob> WeatherFetchJobs => Set<WeatherFetchJob>();
    public DbSet<Zone> Zones => Set<Zone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("operational");
        var enumNameTranslator = new NpgsqlNullNameTranslator();
        modelBuilder.HasPostgresEnum<RiskLevel>("operational", "risk_level_enum", enumNameTranslator);
        modelBuilder.HasPostgresEnum<WeatherFactor>("operational", "weather_factor_enum", enumNameTranslator);
        modelBuilder.HasPostgresEnum<OperationEventType>("operational", "event_type_enum", enumNameTranslator);
        modelBuilder.HasPostgresEnum<OperationMode>("operational", "operation_mode_enum", enumNameTranslator);
        modelBuilder.HasPostgresEnum<UserRole>("operational", "user_role_enum", enumNameTranslator);
        modelBuilder.HasPostgresEnum<UserStatus>("operational", "user_status_enum", enumNameTranslator);
        modelBuilder.HasPostgresEnum<ZoneType>("operational", "zone_type_enum", enumNameTranslator);
        modelBuilder.HasPostgresEnum<SopActionType>("operational", "sop_action_type_enum", enumNameTranslator);
        modelBuilder.HasPostgresEnum<AlertSeverity>("operational", "alert_severity_enum", enumNameTranslator);

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
            entity.Property(x => x.OpenWeatherIcon).HasColumnName("ow_weather_icon");
            entity.Property(x => x.ObservedAt).HasColumnName("observed_at");
            entity.Property(x => x.RecordedAt).HasColumnName("recorded_at");
            entity.Property(x => x.DataSource).HasColumnName("data_source");
            entity.Property(x => x.RawPayload).HasColumnName("raw_payload").HasColumnType("jsonb");
            entity.Property(x => x.IsSimulation).HasColumnName("is_simulation");
            entity.HasOne(x => x.Port).WithMany().HasForeignKey(x => x.PortId);
        });

        modelBuilder.Entity<WeatherFetchJob>(entity =>
        {
            entity.ToTable("weather_fetch_jobs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.PortId).HasColumnName("port_id");
            entity.Property(x => x.SourceId).HasColumnName("source_id");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.StartedAt).HasColumnName("started_at");
            entity.Property(x => x.CompletedAt).HasColumnName("completed_at");
            entity.Property(x => x.ResponseTimeMs).HasColumnName("response_time_ms");
            entity.Property(x => x.HttpStatusCode).HasColumnName("http_status_code");
            entity.Property(x => x.ErrorMessage).HasColumnName("error_message");
            entity.Property(x => x.CreatedReadingId).HasColumnName("created_reading_id");
            entity.Property(x => x.PrefectFlowRunId).HasColumnName("prefect_flow_run_id");
            entity.HasOne(x => x.Port).WithMany().HasForeignKey(x => x.PortId);
            entity.HasOne(x => x.CreatedReading).WithMany().HasForeignKey(x => x.CreatedReadingId);
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

        modelBuilder.Entity<RiskAssessmentDetail>(entity =>
        {
            entity.ToTable("risk_assessment_details");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.AssessmentId).HasColumnName("assessment_id");
            entity.Property(x => x.Factor).HasColumnName("factor");
            entity.Property(x => x.RawValue).HasColumnName("raw_value").HasPrecision(10, 3);
            entity.Property(x => x.BeaufortNumber).HasColumnName("beaufort_number");
            entity.Property(x => x.RiskLevel).HasColumnName("risk_level");
            entity.Property(x => x.Unit).HasColumnName("unit");
            entity.Property(x => x.ThresholdApplied).HasColumnName("threshold_applied");
            entity.HasOne(x => x.Assessment)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.AssessmentId);
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

        modelBuilder.Entity<SopRule>(entity =>
        {
            entity.ToTable("sop_rules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.RuleName).HasColumnName("rule_name").HasMaxLength(255).IsRequired();
            entity.Property(x => x.TriggerRiskLevel).HasColumnName("trigger_risk_level");
            entity.Property(x => x.AppliesToZoneType).HasColumnName("applies_to_zone_type");
            entity.Property(x => x.ActionType).HasColumnName("action_type");
            entity.Property(x => x.ActionDescription).HasColumnName("action_description").IsRequired();
            entity.Property(x => x.TargetOperationMode).HasColumnName("target_operation_mode");
            entity.Property(x => x.ExecutionOrder).HasColumnName("execution_order");
            entity.Property(x => x.AlertMessage).HasColumnName("alert_message");
            entity.Property(x => x.AlertSeverity).HasColumnName("alert_severity");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
        });

        modelBuilder.Entity<SopExecution>(entity =>
        {
            entity.ToTable("sop_executions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.RuleId).HasColumnName("rule_id");
            entity.Property(x => x.RiskAssessmentId).HasColumnName("risk_assessment_id");
            entity.Property(x => x.PortId).HasColumnName("port_id");
            entity.Property(x => x.ZoneId).HasColumnName("zone_id");
            entity.Property(x => x.ExecutedAt).HasColumnName("executed_at");
            entity.Property(x => x.ExecutionResult).HasColumnName("execution_result").HasColumnType("jsonb");
            entity.Property(x => x.SkipReason).HasColumnName("skip_reason");
            entity.Property(x => x.DurationMs).HasColumnName("duration_ms");
            entity.Property(x => x.IsSimulation).HasColumnName("is_simulation");
            entity.HasOne(x => x.Rule).WithMany().HasForeignKey(x => x.RuleId);
            entity.HasOne(x => x.RiskAssessment).WithMany().HasForeignKey(x => x.RiskAssessmentId);
            entity.HasOne(x => x.Port).WithMany().HasForeignKey(x => x.PortId);
            entity.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId);
        });

        modelBuilder.Entity<SimulationSession>(entity =>
        {
            entity.ToTable("simulation_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.PortId).HasColumnName("port_id");
            entity.Property(x => x.StartedByUserId).HasColumnName("started_by_user_id");
            entity.Property(x => x.ScenarioName).HasColumnName("scenario_name").HasMaxLength(255);
            entity.Property(x => x.SpeedMultiplier).HasColumnName("speed_multiplier");
            entity.Property(x => x.TotalSnapshots).HasColumnName("total_snapshots");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            entity.Property(x => x.StartedAt).HasColumnName("started_at");
            entity.Property(x => x.EndedAt).HasColumnName("ended_at");
            entity.HasOne(x => x.Port).WithMany().HasForeignKey(x => x.PortId);
            entity.HasOne(x => x.StartedByUser).WithMany().HasForeignKey(x => x.StartedByUserId);
        });

        modelBuilder.Entity<OperationModeLog>(entity =>
        {
            entity.ToTable("operation_mode_log");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.PortId).HasColumnName("port_id");
            entity.Property(x => x.PreviousMode).HasColumnName("previous_mode");
            entity.Property(x => x.NewMode).HasColumnName("new_mode");
            entity.Property(x => x.TriggeredByRiskLevel).HasColumnName("triggered_by_risk_level");
            entity.Property(x => x.TriggeredBySopRuleId).HasColumnName("triggered_by_sop_rule_id");
            entity.Property(x => x.OverriddenByUserId).HasColumnName("overridden_by_user_id");
            entity.Property(x => x.OverrideReason).HasColumnName("override_reason");
            entity.Property(x => x.ChangeType).HasColumnName("change_type").HasMaxLength(20);
            entity.Property(x => x.ChangedAt).HasColumnName("changed_at");
            entity.Property(x => x.IsSimulation).HasColumnName("is_simulation");
            entity.HasOne(x => x.Port).WithMany().HasForeignKey(x => x.PortId);
            entity.HasOne(x => x.TriggeredBySopRule).WithMany().HasForeignKey(x => x.TriggeredBySopRuleId);
        });

        modelBuilder.Entity<TaskLog>(entity =>
        {
            entity.ToTable("task_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.PortId).HasColumnName("port_id");
            entity.Property(x => x.ZoneId).HasColumnName("zone_id");
            entity.Property(x => x.TriggeredByRuleId).HasColumnName("triggered_by_rule_id");
            entity.Property(x => x.TriggeredByAssessmentId).HasColumnName("triggered_by_assessment_id");
            entity.Property(x => x.ActionType).HasColumnName("action_type");
            entity.Property(x => x.ActionDescription).HasColumnName("action_description");
            entity.Property(x => x.RiskLevelAtCreation).HasColumnName("risk_level_at_creation");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.IsSimulation).HasColumnName("is_simulation");
            entity.HasOne(x => x.Port).WithMany().HasForeignKey(x => x.PortId);
            entity.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId);
            entity.HasOne(x => x.TriggeredByRule).WithMany().HasForeignKey(x => x.TriggeredByRuleId);
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.ToTable("alerts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.PortId).HasColumnName("port_id");
            entity.Property(x => x.AlertType).HasColumnName("alert_type").HasMaxLength(50);
            entity.Property(x => x.Severity).HasColumnName("severity");
            entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(255);
            entity.Property(x => x.Message).HasColumnName("message");
            entity.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(x => x.RelatedSopRuleId).HasColumnName("related_sop_rule_id");
            entity.Property(x => x.RelatedAssessmentId).HasColumnName("related_assessment_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.ReadAt).HasColumnName("read_at");
            entity.Property(x => x.ReadByUserId).HasColumnName("read_by_user_id");
            entity.Property(x => x.IsSimulation).HasColumnName("is_simulation");
            entity.HasOne(x => x.Port).WithMany().HasForeignKey(x => x.PortId);
            entity.HasOne(x => x.RelatedSopRule).WithMany().HasForeignKey(x => x.RelatedSopRuleId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(255).IsRequired();
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            entity.Property(x => x.Role).HasColumnName("role");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.AssignedPortId).HasColumnName("assigned_port_id");
            entity.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
            entity.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(255);
            entity.Property(x => x.RefreshTokenExpiresAt).HasColumnName("refresh_token_expires_at");
            entity.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(x => x.FailedLoginCount).HasColumnName("failed_login_count");
            entity.Property(x => x.LockedUntil).HasColumnName("locked_until");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasOne<Port>()
                .WithMany()
                .HasForeignKey(x => x.AssignedPortId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Zone>(entity =>
        {
            entity.ToTable("zones");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.PortId).HasColumnName("port_id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(x => x.ZoneType).HasColumnName("zone_type");
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.Capacity).HasColumnName("capacity");
            entity.Property(x => x.Latitude).HasColumnName("latitude").HasPrecision(9, 6);
            entity.Property(x => x.Longitude).HasColumnName("longitude").HasPrecision(9, 6);
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CurrentRiskLevel).HasColumnName("current_risk_level");
            entity.Property(x => x.DisplayOrder).HasColumnName("display_order");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne<Port>()
                .WithMany()
                .HasForeignKey(x => x.PortId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
