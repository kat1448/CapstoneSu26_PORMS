using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Npgsql.NameTranslation;
using PORMS.API.BackgroundServices;
using PORMS.API.Configuration;
using PORMS.API.Middleware;
using PORMS.Application.Common.Events;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.Services.Alert;
using PORMS.Application.Services.Mode;
using PORMS.Application.Services.Auths;
using PORMS.Application.Services.Ports;
using PORMS.Application.Services.Risk;
using PORMS.Application.Services.Sop;
using PORMS.Application.Services.Tasks;
using PORMS.Application.Services.Users;
using PORMS.Application.Services.Weather;
using PORMS.Domain.Enums;
using PORMS.Infrastructure.Data;
using PORMS.Infrastructure.Events;
using PORMS.Infrastructure.Security;
using PORMS.Infrastructure.Weather;
using System.Text;
using System.Text.Json.Serialization;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var apiPassword = Environment.GetEnvironmentVariable("POSTGRES_API_PASSWORD") ?? "ApiPass123!";
    connectionString =
        $"Host=localhost;Port=5432;Database=porms_db;Username=porms_api;Password={apiPassword};Include Error Detail=true";
}

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
var enumNameTranslator = new NpgsqlNullNameTranslator();
dataSourceBuilder.MapEnum<RiskLevel>("operational.risk_level_enum", enumNameTranslator);
dataSourceBuilder.MapEnum<WeatherFactor>("operational.weather_factor_enum", enumNameTranslator);
dataSourceBuilder.MapEnum<OperationEventType>("operational.event_type_enum", enumNameTranslator);
dataSourceBuilder.MapEnum<OperationMode>("operational.operation_mode_enum", enumNameTranslator);
dataSourceBuilder.MapEnum<UserRole>("operational.user_role_enum", enumNameTranslator);
dataSourceBuilder.MapEnum<UserStatus>("operational.user_status_enum", enumNameTranslator);
dataSourceBuilder.MapEnum<ZoneType>("operational.zone_type_enum", enumNameTranslator);
dataSourceBuilder.MapEnum<SopActionType>("operational.sop_action_type_enum", enumNameTranslator);
dataSourceBuilder.MapEnum<AlertSeverity>("operational.alert_severity_enum", enumNameTranslator);
var dataSource = dataSourceBuilder.Build();

builder.Services.AddSingleton(dataSource);

builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>());
});

builder.Services.AddScoped<IApplicationDbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IThresholdLoader, ThresholdLoader>();
builder.Services.AddScoped<IRiskAssessmentRepository, RiskAssessmentRepository>();
builder.Services.AddScoped<IRiskEvaluationService, RiskEvaluationService>();
builder.Services.AddScoped<IRiskThresholdService, RiskThresholdService>();
builder.Services.AddScoped<IRiskEngine, RiskEngine>();
builder.Services.AddScoped<IWeatherService, OpenWeatherService>();
builder.Services.AddScoped<IOperationModeService, OperationModeService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<ITaskGeneratorService, TaskGeneratorService>();
builder.Services.AddScoped<ISopEngine, SopEngine>();
builder.Services.AddScoped<IDomainEventPublisher, SopDomainEventPublisher>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPortService, PortService>();

builder.Services.AddHttpClient("OpenWeather", (serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["OpenWeather:BaseUrl"]
        ?? Environment.GetEnvironmentVariable("OW_BASE_URL")
        ?? "https://api.openweathermap.org/data/2.5";

    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(configuration.GetValue("OpenWeather:TimeoutSeconds", 10));
});

//builder.Services.AddHostedService<WeatherUpdateWorker>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtSection["SecretKey"]
                ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                ?? throw new InvalidOperationException(
                    "JWT secret key not configured. Set Jwt:SecretKey in appsettings.Development.json or JWT_SECRET_KEY env var.");

//JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
//JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role",
            NameClaimType = "user_id"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole(nameof(UserRole.ADMIN)));

    options.AddPolicy("AdminOrCompanyAdmin", policy =>
        policy.RequireRole(nameof(UserRole.ADMIN), nameof(UserRole.COMPANY_ADMIN)));
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PORMS API",
        Version = "v1",
        Description = "Port Operation Risk Management System - REST API."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer <token>' to call endpoints that require authentication."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("FrontendDev");

app.UseAuthentication();
app.UseMiddleware<JwtMiddleware>();
app.UseAuthorization();
app.UseMiddleware<RbacMiddleware>();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
