using PORMS.API.Configuration;
using PORMS.API.Middleware;
using Microsoft.Extensions.Options;
using PORMS.Infrastructure.Data;
using PORMS.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));
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
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Run();

public partial class Program
{
}
