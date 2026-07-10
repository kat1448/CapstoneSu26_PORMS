namespace PORMS.API.Configuration;

public sealed class LlmOptions
{
    public const string SectionName = "LLM";

    public string Provider { get; set; } = "Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-flash-latest";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}
