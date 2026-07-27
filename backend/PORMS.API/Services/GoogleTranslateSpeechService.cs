using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Services;

public sealed partial class GoogleTranslateSpeechService
{
    private const int MaximumTextLength = 190;
    private readonly HttpClient _httpClient;

    public GoogleTranslateSpeechService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<byte[]> SynthesizeAlertAsync(
        AlertReadModel alert,
        CancellationToken cancellationToken)
    {
        var text = BuildSpeechText(alert);
        var url = QueryHelpers.AddQueryString(
            "https://translate.google.com/translate_tts",
            new Dictionary<string, string?>
            {
                ["client"] = "tw-ob",
                ["ie"] = "UTF-8",
                ["q"] = text,
                ["tl"] = "vi"
            });

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 PORMS/1.0");
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, "audio/mpeg", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The speech provider did not return MPEG audio.");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    internal static string BuildSpeechText(AlertReadModel alert)
    {
        var firstSentence = alert.Message.Split(". ", 2, StringSplitOptions.TrimEntries)[0].Trim();
        if (!firstSentence.EndsWith('.'))
        {
            firstSentence += ".";
        }

        var action = string.Equals(alert.Severity, "CRITICAL", StringComparison.OrdinalIgnoreCase)
            ? " Rủi ro rất cao. Tạm dừng vận hành và ứng phó khẩn cấp."
            : " Rủi ro cao. Hạn chế vận hành và thực hiện quy trình ứng phó.";
        var text = $"Chú ý. {firstSentence}{action}"
            .Replace("m/s", "mét trên giây", StringComparison.OrdinalIgnoreCase)
            .Replace("mm/giờ", "mi li mét mỗi giờ", StringComparison.OrdinalIgnoreCase)
            .Replace("mm/h", "mi li mét mỗi giờ", StringComparison.OrdinalIgnoreCase)
            .Replace("km", "ki lô mét", StringComparison.OrdinalIgnoreCase);
        text = DecimalNumberRegex().Replace(text, "$1 phẩy $2");

        if (text.Length <= MaximumTextLength)
        {
            return text;
        }

        var shortened = text[..MaximumTextLength];
        var lastSpace = shortened.LastIndexOf(' ');
        return $"{shortened[..Math.Max(lastSpace, 1)].TrimEnd(' ', ',', ';', ':')}.";
    }

    [GeneratedRegex(@"(\d)\.(\d)")]
    private static partial Regex DecimalNumberRegex();
}
