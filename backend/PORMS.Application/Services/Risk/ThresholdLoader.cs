using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PORMS.Application.Common.Interfaces;
using PORMS.Domain.Entities;
using PORMS.Domain.Enums;

namespace PORMS.Application.Services.Risk;

public sealed class ThresholdLoader : IThresholdLoader
{
    private const string CacheKey = "thresholds:global";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IApplicationDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public ThresholdLoader(IApplicationDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<IReadOnlyList<RiskThreshold>> GetThresholdsAsync(
        Guid portId,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<RiskThreshold>? cached) && cached is not null)
        {
            return cached;
        }

        var thresholds = await _dbContext.RiskThresholds
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Factor)
            .ThenBy(x => x.MinValue)
            .ToListAsync(cancellationToken);

        if (thresholds.Count == 0)
        {
            thresholds = GetFallbackThresholds();
        }

        _cache.Set(CacheKey, thresholds, CacheTtl);
        return thresholds;
    }

    public void InvalidateCache()
        => _cache.Remove(CacheKey);

    private static List<RiskThreshold> GetFallbackThresholds()
        =>
        [
            New(WeatherFactor.WIND, RiskLevel.LOW, 0m, 10.8m, "m/s"),
            New(WeatherFactor.WIND, RiskLevel.MEDIUM, 10.8m, 17.2m, "m/s"),
            New(WeatherFactor.WIND, RiskLevel.HIGH, 17.2m, 24.5m, "m/s"),
            New(WeatherFactor.WIND, RiskLevel.CRITICAL, 24.5m, null, "m/s"),
            New(WeatherFactor.RAIN, RiskLevel.LOW, 0m, 10m, "mm/h"),
            New(WeatherFactor.RAIN, RiskLevel.MEDIUM, 10m, 25m, "mm/h"),
            New(WeatherFactor.RAIN, RiskLevel.HIGH, 25m, 50m, "mm/h"),
            New(WeatherFactor.RAIN, RiskLevel.CRITICAL, 50m, null, "mm/h"),
            New(WeatherFactor.VISIBILITY, RiskLevel.CRITICAL, 0m, 1m, "km"),
            New(WeatherFactor.VISIBILITY, RiskLevel.HIGH, 1m, 5m, "km"),
            New(WeatherFactor.VISIBILITY, RiskLevel.MEDIUM, 5m, 10m, "km"),
            New(WeatherFactor.VISIBILITY, RiskLevel.LOW, 10m, null, "km")
        ];

    private static RiskThreshold New(
        WeatherFactor factor,
        RiskLevel riskLevel,
        decimal minValue,
        decimal? maxValue,
        string unit)
        => new()
        {
            Id = Guid.Empty,
            Factor = factor,
            RiskLevel = riskLevel,
            MinValue = minValue,
            MaxValue = maxValue,
            Unit = unit,
            IsActive = true
        };
}
