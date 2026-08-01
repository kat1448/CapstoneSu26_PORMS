using PORMS.API.Contracts;

namespace PORMS.API.Services;

public sealed class ForecastRiskMlService
{
    public ForecastRiskAnalysisResponse Analyze(ForecastRiskAnalysisRequest request)
    {
        var items = request.Items.ToList();
        var vectors = items.Select(ToFeatureVector).ToList();
        var normalized = Normalize(vectors);
        var pcaScores = ProjectFirstPrincipalComponent(normalized);
        var clusters = AssignKMeansClusters(normalized);

        var minScore = pcaScores.Count == 0 ? 0 : pcaScores.Min();
        var maxScore = pcaScores.Count == 0 ? 1 : pcaScores.Max();

        var results = items.Select((item, index) =>
        {
            var score = ScaleScore(pcaScores[index], minScore, maxScore, item);
            var label = LabelCluster(vectors[index], item, score);
            return new ForecastRiskAnalysisItemResponse
            {
                PlannedAt = item.PlannedAt,
                RuleRiskLevel = NormalizeRisk(item.RuleRiskLevel),
                PcaRiskScore = score,
                ClusterId = clusters[index],
                ClusterLabel = label,
                MlRecommendation = Recommendation(label, score, item),
                DominantFactors = DominantFactors(vectors[index], item)
            };
        }).ToList();

        return new ForecastRiskAnalysisResponse
        {
            PortCode = string.IsNullOrWhiteSpace(request.PortCode) ? "N/A" : request.PortCode.Trim().ToUpperInvariant(),
            ModelVersion = "pca-kmeans-v1",
            Items = results
        };
    }

    private static double[] ToFeatureVector(ForecastRiskAnalysisItemRequest item)
    {
        var windRisk = RiskScore(item.WindRiskLevel);
        var rainRisk = RiskScore(item.RainRiskLevel);
        var visibilityRisk = RiskScore(item.VisibilityRiskLevel);
        var finalRisk = RiskScore(item.RuleRiskLevel);
        var wind = item.WindSpeedMs ?? windRisk * 5;
        var rain = item.RainfallMm ?? rainRisk * 8;
        var visibilityStress = item.VisibilityKm is null ? visibilityRisk * 2.5 : Math.Max(0, 10 - item.VisibilityKm.Value);
        var humidity = item.HumidityPct ?? 65 + rainRisk * 7;
        var pressureDrop = item.PressureHpa is null ? finalRisk * 2 : Math.Max(0, 1013 - item.PressureHpa.Value);
        var temperatureStress = item.TemperatureC is null ? 0 : Math.Abs(item.TemperatureC.Value - 28);

        return [wind, rain, visibilityStress, humidity, pressureDrop, temperatureStress, windRisk, rainRisk, visibilityRisk, finalRisk];
    }

    private static List<double[]> Normalize(IReadOnlyList<double[]> vectors)
    {
        if (vectors.Count == 0) return [];

        var width = vectors[0].Length;
        var means = Enumerable.Range(0, width).Select(column => vectors.Average(row => row[column])).ToArray();
        var deviations = Enumerable.Range(0, width)
            .Select(column =>
            {
                var variance = vectors.Average(row => Math.Pow(row[column] - means[column], 2));
                return Math.Sqrt(variance) is var deviation && deviation > 0.0001 ? deviation : 1;
            })
            .ToArray();

        return vectors.Select(row => row.Select((value, column) => (value - means[column]) / deviations[column]).ToArray()).ToList();
    }

    private static List<double> ProjectFirstPrincipalComponent(IReadOnlyList<double[]> normalized)
    {
        if (normalized.Count == 0) return [];
        if (normalized.Count == 1) return [0];

        var width = normalized[0].Length;
        var component = Enumerable.Repeat(1.0 / Math.Sqrt(width), width).ToArray();

        for (var iteration = 0; iteration < 20; iteration++)
        {
            var next = new double[width];
            foreach (var row in normalized)
            {
                var projection = Dot(row, component);
                for (var column = 0; column < width; column++)
                {
                    next[column] += projection * row[column];
                }
            }

            var norm = Math.Sqrt(next.Sum(value => value * value));
            if (norm <= 0.0001) break;
            component = next.Select(value => value / norm).ToArray();
        }

        var scores = normalized.Select(row => Dot(row, component)).ToList();
        var severity = normalized.Select(row => row[^1]).ToArray();
        if (Correlation(scores, severity) < 0)
        {
            scores = scores.Select(score => -score).ToList();
        }

        return scores;
    }

    private static List<int> AssignKMeansClusters(IReadOnlyList<double[]> normalized)
    {
        if (normalized.Count == 0) return [];
        var k = Math.Min(4, normalized.Count);
        var centroids = normalized.Take(k).Select(row => row.ToArray()).ToList();
        var assignments = Enumerable.Repeat(0, normalized.Count).ToArray();

        for (var iteration = 0; iteration < 12; iteration++)
        {
            var changed = false;
            for (var index = 0; index < normalized.Count; index++)
            {
                var cluster = Enumerable.Range(0, k)
                    .OrderBy(candidate => Distance(normalized[index], centroids[candidate]))
                    .First();
                if (assignments[index] != cluster)
                {
                    assignments[index] = cluster;
                    changed = true;
                }
            }

            for (var cluster = 0; cluster < k; cluster++)
            {
                var members = normalized.Where((_, index) => assignments[index] == cluster).ToList();
                if (members.Count == 0) continue;
                centroids[cluster] = Enumerable.Range(0, normalized[0].Length)
                    .Select(column => members.Average(row => row[column]))
                    .ToArray();
            }

            if (!changed) break;
        }

        return assignments.ToList();
    }

    private static int ScaleScore(double score, double minScore, double maxScore, ForecastRiskAnalysisItemRequest item)
    {
        var pca = Math.Abs(maxScore - minScore) <= 0.0001 ? RiskScore(item.RuleRiskLevel) * 25 : ((score - minScore) / (maxScore - minScore)) * 100;
        var ruleAnchor = RiskScore(item.RuleRiskLevel) switch
        {
            1 => 18,
            2 => 45,
            3 => 72,
            _ => 92
        };
        return Math.Clamp((int)Math.Round(pca * 0.45 + ruleAnchor * 0.55), 0, 100);
    }

    private static string LabelCluster(double[] vector, ForecastRiskAnalysisItemRequest item, int score)
    {
        var wind = vector[0];
        var rain = vector[1];
        var visibilityStress = vector[2];
        var finalRisk = RiskScore(item.RuleRiskLevel);

        if (score >= 85 || finalRisk >= 4) return "SEVERE_OPERATION_RISK";
        if (rain >= wind && visibilityStress >= wind && (rain >= 12 || visibilityStress >= 4)) return "RAIN_VISIBILITY_RISK";
        if (wind >= 10 || RiskScore(item.WindRiskLevel) >= 3) return "WIND_RISK";
        return "STABLE_WEATHER";
    }

    private static string Recommendation(string label, int score, ForecastRiskAnalysisItemRequest item)
    {
        if (label == "SEVERE_OPERATION_RISK" || score >= 75 || NormalizeRisk(item.RuleRiskLevel) == "CRITICAL") return "STOP";
        if (score >= 50 || NormalizeRisk(item.RuleRiskLevel) == "HIGH") return "LIMITED";
        return "NORMAL";
    }

    private static IReadOnlyList<string> DominantFactors(double[] vector, ForecastRiskAnalysisItemRequest item)
    {
        var factors = new List<(string Name, double Value)>
        {
            ("WIND", vector[0] + RiskScore(item.WindRiskLevel) * 2),
            ("RAIN", vector[1] + RiskScore(item.RainRiskLevel) * 2),
            ("VISIBILITY", vector[2] + RiskScore(item.VisibilityRiskLevel) * 2)
        };

        return factors
            .OrderByDescending(factor => factor.Value)
            .Take(2)
            .Select(factor => factor.Name)
            .ToList();
    }

    private static int RiskScore(string riskLevel) => NormalizeRisk(riskLevel) switch
    {
        "CRITICAL" => 4,
        "HIGH" => 3,
        "MEDIUM" => 2,
        _ => 1
    };

    private static string NormalizeRisk(string? riskLevel)
    {
        var normalized = (riskLevel ?? "LOW").Trim().ToUpperInvariant();
        return normalized is "LOW" or "MEDIUM" or "HIGH" or "CRITICAL" ? normalized : "LOW";
    }

    private static double Dot(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        var total = 0.0;
        for (var index = 0; index < left.Count; index++)
        {
            total += left[index] * right[index];
        }

        return total;
    }

    private static double Distance(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        var total = 0.0;
        for (var index = 0; index < left.Count; index++)
        {
            total += Math.Pow(left[index] - right[index], 2);
        }

        return Math.Sqrt(total);
    }

    private static double Correlation(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        var leftMean = left.Average();
        var rightMean = right.Average();
        var numerator = left.Select((value, index) => (value - leftMean) * (right[index] - rightMean)).Sum();
        var denominator = Math.Sqrt(left.Sum(value => Math.Pow(value - leftMean, 2)) * right.Sum(value => Math.Pow(value - rightMean, 2)));
        return denominator <= 0.0001 ? 0 : numerator / denominator;
    }
}
