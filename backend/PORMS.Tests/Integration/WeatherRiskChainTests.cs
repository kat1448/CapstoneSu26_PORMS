using Microsoft.Extensions.DependencyInjection;
using PORMS.Infrastructure.Repositories;
using PORMS.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace PORMS.Tests.Integration
{
    [Collection(DatabaseBackedIntegrationCollection.Name)]
    public sealed class WeatherRiskChainTests
    {
        private readonly IntegrationTestWebApplicationFactory _factory;

        public WeatherRiskChainTests(
            IntegrationTestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task EvaluateWeatherReading_CreatesAssessmentAndIsIdempotent()
        {
            var weather = await _factory.SeedLiveWeatherReadingAsync();

            try
            {
                using var scope = _factory.Services.CreateScope();

                var liveRiskRepository =
                    scope.ServiceProvider
                        .GetRequiredService<LiveRiskAssessmentRepository>();

                var riskRepository =
                    scope.ServiceProvider
                        .GetRequiredService<RiskRepository>();

                var evaluator =
                    scope.ServiceProvider
                        .GetRequiredService<RiskThresholdEvaluator>();

                var portRepository =
                    scope.ServiceProvider
                        .GetRequiredService<PortRepository>();

                // Tính kết quả mong đợi từ cấu hình thật trong database
                var configuredThresholds =
                    await riskRepository.GetVersionOneThresholdsAsync(
                        CancellationToken.None);

                var evaluatorThresholds = configuredThresholds
                    .Select(item => new RiskThresholdRule(
                        item.Factor,
                        item.RiskLevel,
                        item.ComparisonOperator,
                        item.ThresholdValue,
                        item.IsEnabled))
                    .ToList();

                var expected = evaluator.Evaluate(
                    new WeatherRiskInput(
                        BeaufortNumber: 8,
                        Rainfall1hMm: 30m,
                        VisibilityKm: 2m),
                    evaluatorThresholds);

                // Lần đầu phải tạo assessment mới
                var created =
                    await liveRiskRepository.EvaluateWeatherReadingAsync(
                        weather.PortId,
                        weather.WeatherReadingId,
                        CancellationToken.None);

                Assert.NotNull(created);
                Assert.True(created!.Created);
                Assert.Equal(weather.PortId, created.PortId);
                Assert.Equal(
                    weather.WeatherReadingId,
                    created.WeatherReadingId);
                Assert.Equal(expected.Wind.RiskLevel, created.WindRiskLevel);
                Assert.Equal(expected.Rain.RiskLevel, created.RainRiskLevel);
                Assert.Equal(
                    expected.Visibility.RiskLevel,
                    created.VisibilityRiskLevel);
                Assert.Equal(expected.FinalRiskLevel, created.FinalRiskLevel);
                Assert.Equal("LOW", created.PreviousRiskLevel);
                Assert.Equal(expected.DominantFactor, created.DominantFactor);
                Assert.Equal(expected.Summary, created.Summary);
                Assert.Equal(
                    expected.FinalRiskLevel != "LOW",
                    created.LevelChanged);

                // Trigger database phải cập nhật risk hiện tại của cảng
                var portState = await portRepository.GetPortAsync(
                    weather.PortId,
                    CancellationToken.None);

                Assert.NotNull(portState);
                Assert.Equal(
                    expected.FinalRiskLevel,
                    portState!.CurrentRiskLevel);

                // Retry cùng weather reading phải trả assessment cũ
                var retried =
                    await liveRiskRepository.EvaluateWeatherReadingAsync(
                        weather.PortId,
                        weather.WeatherReadingId,
                        CancellationToken.None);

                Assert.NotNull(retried);
                Assert.False(retried!.Created);
                Assert.Equal(
                    created.RiskAssessmentId,
                    retried.RiskAssessmentId);
                Assert.Equal(
                    created.FinalRiskLevel,
                    retried.FinalRiskLevel);
            }
            finally
            {
                // Luôn dọn dữ liệu kể cả khi assertion thất bại
                await _factory.CleanupLiveWeatherReadingAsync(
                    weather.PortId,
                    weather.WeatherReadingId);
            }
        }
    }
}
