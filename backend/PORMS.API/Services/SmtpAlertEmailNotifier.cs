using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using PORMS.API.Configuration;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Services;

public sealed class SmtpAlertEmailNotifier : IAlertEmailNotifier
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpAlertEmailNotifier> _logger;

    public SmtpAlertEmailNotifier(IOptions<EmailOptions> options, ILogger<SmtpAlertEmailNotifier> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendHighSeverityAlertAsync(AlertNotificationReadModel alert, CancellationToken cancellationToken)
    {
        var smtp = _options.Smtp;
        if (!smtp.Enabled || alert.RecipientEmails.Count == 0) return;
        if (string.IsNullOrWhiteSpace(smtp.Host) || string.IsNullOrWhiteSpace(smtp.FromEmail))
        {
            _logger.LogWarning("Alert email skipped because SMTP host or sender is missing. AlertId={AlertId}", alert.AlertId);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(smtp.FromEmail, smtp.FromName, Encoding.UTF8),
            Subject = $"PORMS - Cảnh báo {RiskLabel(alert.Severity)} tại {alert.PortName}",
            Body = BuildBody(alert),
            IsBodyHtml = false,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            HeadersEncoding = Encoding.UTF8
        };
        foreach (var email in alert.RecipientEmails.Where(item => !string.IsNullOrWhiteSpace(item)))
            message.To.Add(new MailAddress(email));

        using var client = new SmtpClient(smtp.Host, smtp.Port) { EnableSsl = smtp.EnableSsl };
        if (!string.IsNullOrWhiteSpace(smtp.Username)) client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
        await client.SendMailAsync(message, cancellationToken);
    }

    private static string BuildBody(AlertNotificationReadModel alert) => $"""
        Xin chào,

        PORMS vừa phát hiện cảnh báo {RiskLabel(alert.Severity)} tại cảng bạn phụ trách.

        Cảng: {alert.PortCode} - {alert.PortName}
        Khu vực: {alert.ZoneName}
        Nội dung: {alert.Message}
        Thời điểm: {alert.CreatedAt.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy HH:mm} (GMT+7)

        Vui lòng đăng nhập PORMS để kiểm tra cảnh báo và phân công nhiệm vụ cho Operator phù hợp.
        """;

    private static string RiskLabel(string severity) => severity switch
    {
        "CRITICAL" => "rất cao",
        "HIGH" => "cao",
        _ => severity
    };
}
