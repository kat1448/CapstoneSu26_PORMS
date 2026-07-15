using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PORMS.API.Configuration;
using PORMS.API.Contracts;

namespace PORMS.API.Services;

public sealed class OperationPlanLlmService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string FallbackGeminiModel = "gemini-flash-latest";
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<OperationPlanLlmService> _logger;

    public OperationPlanLlmService(HttpClient httpClient, IOptions<LlmOptions> options, ILogger<OperationPlanLlmService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OperationPlanAnalysisResponse> AnalyzeAsync(
        ForecastRiskAnalysisRequest request,
        ForecastRiskAnalysisResponse mlAnalysis,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            foreach (var model in CandidateModels())
            {
                try
                {
                    var llmResult = await AnalyzeWithGeminiAsync(model, request, mlAnalysis, cancellationToken);
                    if (llmResult is not null)
                    {
                        return llmResult;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LLM operation plan analysis failed for model {Model}.", model);
                }
            }
        }

        return BuildFallbackPlan(request, mlAnalysis, !string.IsNullOrWhiteSpace(_options.ApiKey));
    }

    private async Task<OperationPlanAnalysisResponse?> AnalyzeWithGeminiAsync(
        string model,
        ForecastRiskAnalysisRequest request,
        ForecastRiskAnalysisResponse mlAnalysis,
        CancellationToken cancellationToken)
    {
        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/models/{model}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";
        var prompt = BuildPrompt(request, mlAnalysis);
        var geminiRequest = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.2,
                maxOutputTokens = 4096,
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        summary = new { type = "STRING" },
                        items = new
                        {
                            type = "ARRAY",
                            items = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    plannedAt = new { type = "STRING" },
                                    operationMode = new { type = "STRING", @enum = new[] { "NORMAL", "LIMITED", "STOP" } },
                                    planChange = new { type = "STRING" },
                                    reason = new { type = "STRING" },
                                    recommendedActions = new
                                    {
                                        type = "ARRAY",
                                        items = new { type = "STRING" }
                                    },
                                    affectedOperations = new
                                    {
                                        type = "ARRAY",
                                        items = new { type = "STRING" }
                                    }
                                },
                                required = new[] { "plannedAt", "operationMode", "planChange", "reason", "recommendedActions", "affectedOperations" }
                            }
                        }
                    },
                    required = new[] { "summary", "items" }
                }
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(endpoint, geminiRequest, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var text = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var json = ExtractJsonObject(text);
        using var payload = JsonDocument.Parse(json);
        var root = payload.RootElement;
        var sourceItems = request.Items.Take(5).ToList();
        var items = root.GetProperty("items").EnumerateArray()
            .Select((item, index) =>
            {
                var mlItem = mlAnalysis.Items.ElementAtOrDefault(index);
                var requestItem = index < sourceItems.Count ? sourceItems[index] : null;
                var requestedMode = NormalizeMode(item.GetProperty("operationMode").GetString());
                var operationMode = mlItem is null ? requestedMode : EnforceMinimumMode(requestedMode, mlItem);
                var wasCorrected = requestedMode != operationMode && mlItem is not null;
                return new OperationPlanAnalysisItemResponse
                {
                    PlannedAt = DateTimeOffset.Parse(item.GetProperty("plannedAt").GetString() ?? DateTimeOffset.UtcNow.ToString("O")),
                    OperationMode = operationMode,
                    PlanChange = wasCorrected ? PlanChange(index + 1, operationMode, mlItem!, requestItem) : RequiredText(item, "planChange"),
                    Reason = AddModeCorrectionReason(RequiredText(item, "reason"), requestedMode, operationMode, mlItem),
                    RecommendedActions = wasCorrected ? RecommendedActions(operationMode, mlItem!, requestItem) : ReadStringArray(item, "recommendedActions"),
                    AffectedOperations = wasCorrected ? AffectedOperations(operationMode, mlItem!, requestItem) : ReadStringArray(item, "affectedOperations")
                };
            })
            .ToList();

        return new OperationPlanAnalysisResponse
        {
            PortCode = mlAnalysis.PortCode,
            Provider = "GEMINI",
            Model = model,
            IsConfigured = true,
            Summary = RequiredText(root, "summary"),
            Items = items
        };
    }

    private IReadOnlyList<string> CandidateModels()
    {
        var configuredModel = NormalizeModelName(_options.Model);
        return configuredModel.Equals(FallbackGeminiModel, StringComparison.OrdinalIgnoreCase)
            ? [configuredModel]
            : [configuredModel, FallbackGeminiModel];
    }

    private static string NormalizeModelName(string? model)
    {
        var normalized = string.IsNullOrWhiteSpace(model) ? FallbackGeminiModel : model.Trim();
        const string prefix = "models/";
        return normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? normalized[prefix.Length..]
            : normalized;
    }

    private OperationPlanAnalysisResponse BuildFallbackPlan(
        ForecastRiskAnalysisRequest request,
        ForecastRiskAnalysisResponse mlAnalysis,
        bool llmConfigured)
    {
        var sourceItems = request.Items.Take(5).ToList();
        var items = mlAnalysis.Items.Select((item, index) =>
        {
            var requestItem = index < sourceItems.Count ? sourceItems[index] : null;
            var mode = ModeFromScoreAndRisk(item.PcaRiskScore, item.RuleRiskLevel);
            return new OperationPlanAnalysisItemResponse
            {
                PlannedAt = item.PlannedAt,
                OperationMode = mode,
                PlanChange = PlanChange(index + 1, mode, item, requestItem),
                Reason = Reason(item, requestItem),
                RecommendedActions = RecommendedActions(mode, item, requestItem),
                AffectedOperations = AffectedOperations(mode, item, requestItem)
            };
        }).ToList();

        return new OperationPlanAnalysisResponse
        {
            PortCode = mlAnalysis.PortCode,
            Provider = llmConfigured ? "LOCAL_RULE_FALLBACK_AFTER_LLM_ERROR" : "LOCAL_RULE_FALLBACK",
            Model = llmConfigured ? _options.Model : "local-operation-planner",
            IsConfigured = llmConfigured,
            Summary = "Kế hoạch vận hành thay đổi theo xu hướng thời tiết, điểm PCA/K-Means và mức rủi ro từng ngày.",
            Items = items
        };
    }

    private static string BuildPrompt(ForecastRiskAnalysisRequest request, ForecastRiskAnalysisResponse mlAnalysis)
    {
        var rows = mlAnalysis.Items.Select((item, index) =>
        {
            var input = request.Items.ElementAtOrDefault(index);
            return new
            {
                day = index + 1,
                item.PlannedAt,
                item.PcaRiskScore,
                item.ClusterLabel,
                item.RuleRiskLevel,
                item.MlRecommendation,
                item.DominantFactors,
                input?.WindSpeedMs,
                input?.RainfallMm,
                input?.VisibilityKm,
                input?.HumidityPct,
                input?.PressureHpa,
                input?.TemperatureC
            };
        });

        return $$"""
        Bạn là trợ lý vận hành cảng biển. Hãy phân tích kế hoạch vận hành 5 ngày dựa trên thời tiết, PCA score và K-Means.
        Chỉ trả JSON hợp lệ, không markdown. Schema:
        {
          "summary": "string",
          "items": [
            {
              "plannedAt": "ISO datetime",
              "operationMode": "NORMAL | LIMITED | STOP",
              "planChange": "string",
              "reason": "string",
              "recommendedActions": ["string"],
              "affectedOperations": ["string"]
            }
          ]
        }
        Quy tắc nghiệp vụ bắt buộc: LOW/MEDIUM vận hành NORMAL; HIGH hoặc PCA score 50-74 phải là LIMITED; CRITICAL hoặc PCA score từ 75 phải là STOP. Không được hạ operationMode thấp hơn rule/PCA.
        Cảng: {{mlAnalysis.PortCode}}
        Dữ liệu: {{JsonSerializer.Serialize(rows, JsonOptions)}}
        """;
    }

    private static string ModeFromScoreAndRisk(int score, string riskLevel)
    {
        var normalized = NormalizeRisk(riskLevel);
        if (normalized == "CRITICAL" || score >= 75) return "STOP";
        if (normalized == "HIGH" || score >= 50) return "LIMITED";
        return "NORMAL";
    }

    private static string EnforceMinimumMode(string requestedMode, ForecastRiskAnalysisItemResponse item)
    {
        var minimumMode = ModeFromScoreAndRisk(item.PcaRiskScore, item.RuleRiskLevel);
        return ModeSeverity(requestedMode) >= ModeSeverity(minimumMode) ? requestedMode : minimumMode;
    }

    private static int ModeSeverity(string mode)
    {
        return NormalizeMode(mode) switch
        {
            "STOP" => 3,
            "LIMITED" => 2,
            _ => 1
        };
    }

    private static string AddModeCorrectionReason(
        string reason,
        string requestedMode,
        string operationMode,
        ForecastRiskAnalysisItemResponse? item)
    {
        if (item is null || requestedMode == operationMode)
        {
            return reason;
        }

        var suffix = $"He thong tu nang che do tu {requestedMode} len {operationMode} vi PCA score {item.PcaRiskScore} va rule risk {item.RuleRiskLevel}.";
        return string.IsNullOrWhiteSpace(reason) ? suffix : $"{reason} {suffix}";
    }

    private static string PlanChange(int day, string mode, ForecastRiskAnalysisItemResponse item, ForecastRiskAnalysisItemRequest? requestItem)
    {
        return mode switch
        {
            "STOP" => $"Ngày {day}: rủi ro CRITICAL, chuyển sang STOP hoặc dừng khu vực chịu ảnh hưởng.",
            "LIMITED" when item.DominantFactors.Contains("RAIN") || item.DominantFactors.Contains("VISIBILITY") => $"Ngày {day}: mưa lớn hoặc tầm nhìn giảm, hạn chế bốc xếp và điều tiết phương tiện.",
            "LIMITED" => $"Ngày {day}: gió hoặc điều kiện thời tiết tăng, hạn chế hoạt động nhạy cảm.",
            _ when requestItem?.WindSpeedMs >= 8 => $"Ngày {day}: thời tiết còn trong ngưỡng vận hành, tăng giám sát gió.",
            _ => $"Ngày {day}: thời tiết ổn, cảng vận hành bình thường."
        };
    }

    private static string Reason(ForecastRiskAnalysisItemResponse item, ForecastRiskAnalysisItemRequest? requestItem)
    {
        var weather = new List<string>();
        if (requestItem?.WindSpeedMs is not null) weather.Add($"gió {requestItem.WindSpeedMs:0.#} m/s");
        if (requestItem?.RainfallMm is not null) weather.Add($"mưa {requestItem.RainfallMm:0.#} mm");
        if (requestItem?.VisibilityKm is not null) weather.Add($"tầm nhìn {requestItem.VisibilityKm:0.#} km");
        var weatherText = weather.Count == 0 ? "dữ liệu thời tiết dự báo" : string.Join(", ", weather);
        return $"{weatherText}; PCA score {item.PcaRiskScore}; cụm {item.ClusterLabel}.";
    }

    private static IReadOnlyList<string> RecommendedActions(string mode, ForecastRiskAnalysisItemResponse item, ForecastRiskAnalysisItemRequest? requestItem)
    {
        if (mode == "STOP")
        {
            return ["Dừng bốc xếp khu vực nguy hiểm", "Thông báo ca trực và đội an toàn", "Kích hoạt SOP ứng phó khẩn cấp"];
        }

        if (mode == "LIMITED")
        {
            return ["Hạn chế bốc xếp hàng nhạy cảm", "Tăng giám sát cầu bến", "Chuẩn bị phương án chuyển ca hoặc dừng cục bộ"];
        }

        if (requestItem?.WindSpeedMs >= 8 || item.PcaRiskScore >= 25)
        {
            return ["Vận hành bình thường", "Tăng tần suất theo dõi thời tiết", "Chuẩn bị phương án hạn chế bốc xếp"];
        }

        return ["Vận hành bình thường", "Theo dõi thời tiết theo lịch", "Duy trì kế hoạch khai thác hiện tại"];
    }

    private static IReadOnlyList<string> AffectedOperations(string mode, ForecastRiskAnalysisItemResponse item, ForecastRiskAnalysisItemRequest? requestItem)
    {
        if (mode == "STOP") return ["Bốc xếp", "Điều độ cầu bến", "Khu vực rủi ro cao"];
        if (mode == "LIMITED") return ["Bốc xếp", "Di chuyển phương tiện", "Giám sát cầu bến"];
        if (requestItem?.WindSpeedMs >= 8) return ["Giám sát cầu bến"];
        return ["Khai thác thường lệ"];
    }

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private static string RequiredText(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array
            ? property.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).Where(item => !string.IsNullOrWhiteSpace(item)).ToList()
            : [];
    }

    private static string NormalizeMode(string? mode)
    {
        var normalized = (mode ?? "NORMAL").Trim().ToUpperInvariant();
        return normalized is "NORMAL" or "LIMITED" or "STOP" ? normalized : "NORMAL";
    }

    private static string NormalizeRisk(string? riskLevel)
    {
        var normalized = (riskLevel ?? "LOW").Trim().ToUpperInvariant();
        return normalized is "LOW" or "MEDIUM" or "HIGH" or "CRITICAL" ? normalized : "LOW";
    }
}
