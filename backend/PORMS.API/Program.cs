using Microsoft.EntityFrameworkCore;
using Npgsql;
using PORMS.API.BackgroundServices;
using PORMS.API.Configuration;
using PORMS.Application.Common.Events;
using PORMS.Application.Common.Interfaces;
using PORMS.Application.Services.Risk;
using PORMS.Application.Services.Weather;
using PORMS.Domain.Enums;
using PORMS.Infrastructure.Data;
using PORMS.Infrastructure.Events;
using PORMS.Infrastructure.Weather;

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
dataSourceBuilder.MapEnum<RiskLevel>("operational.risk_level_enum");
dataSourceBuilder.MapEnum<WeatherFactor>("operational.weather_factor_enum");
dataSourceBuilder.MapEnum<OperationEventType>("operational.event_type_enum");
dataSourceBuilder.MapEnum<OperationMode>("operational.operation_mode_enum");
var dataSource = dataSourceBuilder.Build();

builder.Services.AddSingleton(dataSource);

builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>());
});

builder.Services.AddScoped<IApplicationDbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

builder.Services.AddScoped<IRiskEngine, RiskEngine>();
builder.Services.AddScoped<IWeatherService, OpenWeatherService>();
builder.Services.AddScoped<IDomainEventPublisher, LoggingDomainEventPublisher>();

builder.Services.AddHttpClient("OpenWeather", (serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["OpenWeather:BaseUrl"]
        ?? Environment.GetEnvironmentVariable("OW_BASE_URL")
        ?? "https://api.openweathermap.org/data/2.5";

    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(configuration.GetValue("OpenWeather:TimeoutSeconds", 10));
});

builder.Services.AddHostedService<WeatherUpdateWorker>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
