using PORMS.API.Configuration;
using PORMS.API.Middleware;
using Microsoft.Extensions.Options;
using PORMS.Infrastructure.Data;
using PORMS.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PORMS.API.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<CorsOptions>(
    builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<OpenWeatherOptions>(
    builder.Configuration.GetSection(OpenWeatherOptions.SectionName));
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<LlmOptions>(
    builder.Configuration.GetSection(LlmOptions.SectionName));
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var configuredOrigins = builder.Configuration
            .GetSection($"{CorsOptions.SectionName}:AllowedOrigins")
            .Get<string[]>();

        var origins = configuredOrigins is { Length: > 0 }
            ? configuredOrigins
            : ["http://localhost:5173"];

        policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = !string.IsNullOrWhiteSpace(options.ConnectionString)
        ? options.ConnectionString
        : configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Missing database connection string.");
    }

    return new NpgsqlConnectionFactory(connectionString);
});
builder.Services.AddScoped<DashboardRepository>();
builder.Services.AddScoped<PortRepository>();
builder.Services.AddScoped<AlertRepository>();
builder.Services.AddScoped<OperationEventRepository>();
builder.Services.AddScoped<SimulationRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<WeatherRepository>();
builder.Services.AddScoped<ForecastEvaluationRepository>();
builder.Services.AddScoped<RiskRepository>();
builder.Services.AddScoped<SopRuleRepository>();
builder.Services.AddScoped<TaskRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<ForecastRiskMlService>();
builder.Services.AddScoped<ITaskAssignmentEmailNotifier, SmtpTaskAssignmentEmailNotifier>();
builder.Services.AddHttpClient<OpenWeatherService>();
builder.Services.AddHttpClient<OperationPlanLlmService>();
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing JWT configuration.");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("ADMIN"));
    options.AddPolicy("AdminOrPortManager", policy => policy.RequireRole("ADMIN", "PORT_MANAGER"));
    options.AddPolicy("AllAppUsers", policy => policy.RequireRole("ADMIN", "PORT_MANAGER", "OPERATOR"));
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Run();

public partial class Program
{
}
