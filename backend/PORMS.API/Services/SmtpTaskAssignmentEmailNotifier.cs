using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using PORMS.API.Configuration;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Services;

public sealed class SmtpTaskAssignmentEmailNotifier : ITaskAssignmentEmailNotifier
{
    private readonly EmailOptions _options;
    private readonly string _frontendBaseUrl;
    private readonly ILogger<SmtpTaskAssignmentEmailNotifier> _logger;

    public SmtpTaskAssignmentEmailNotifier(
        IOptions<EmailOptions> options,
        IConfiguration configuration,
        ILogger<SmtpTaskAssignmentEmailNotifier> logger)
    {
        _options = options.Value;
        _frontendBaseUrl = (configuration["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        _logger = logger;
    }

    public async Task SendAssignedTaskEmailAsync(TaskLogReadModel task, CancellationToken cancellationToken)
    {
        var smtp = _options.Smtp;
        if (!smtp.Enabled)
        {
            _logger.LogInformation("Task assignment email is disabled. TaskId={TaskId}", task.TaskId);
            return;
        }

        if (string.IsNullOrWhiteSpace(task.AssignedUserEmail))
        {
            _logger.LogWarning("Task assignment email skipped because assigned user email is empty. TaskId={TaskId}", task.TaskId);
            return;
        }

        ValidateSmtpOptions(smtp);

        using var message = new MailMessage
        {
            From = new MailAddress(smtp.FromEmail, smtp.FromName, Encoding.UTF8),
            Subject = $"PORMS - Nhiệm vụ mới: {task.TaskCode}",
            Body = BuildBody(task, _frontendBaseUrl),
            IsBodyHtml = false,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            HeadersEncoding = Encoding.UTF8
        };
        message.To.Add(new MailAddress(
            task.AssignedUserEmail,
            task.AssignedUserName ?? task.AssignedUserEmail,
            Encoding.UTF8));

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(smtp.Username))
        {
            client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    private static void ValidateSmtpOptions(SmtpEmailOptions smtp)
    {
        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            throw new InvalidOperationException("Email:Smtp:Host is required when SMTP email is enabled.");
        }

        if (string.IsNullOrWhiteSpace(smtp.FromEmail))
        {
            throw new InvalidOperationException("Email:Smtp:FromEmail is required when SMTP email is enabled.");
        }
    }

    private static string BuildBody(TaskLogReadModel task, string frontendBaseUrl)
    {
        var dueAt = task.DueAt is null
            ? "Chưa đặt"
            : $"{task.DueAt.Value.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy HH:mm} (GMT+7)";
        var zone = string.IsNullOrWhiteSpace(task.ZoneName) ? "Toàn cảng" : task.ZoneName;
        var description = string.IsNullOrWhiteSpace(task.Description) ? "Không có mô tả." : task.Description;

        return $"""
            Xin chào {task.AssignedUserName ?? task.AssignedUserEmail},

            Bạn vừa được phân công một nhiệm vụ trên hệ thống PORMS.

            Mã nhiệm vụ: {task.TaskCode}
            Nhiệm vụ: {task.Title}
            Mức độ ưu tiên: {TranslatePriority(task.Priority)}
            Trạng thái: {TranslateStatus(task.Status)}
            Cảng: {task.PortCode} - {task.PortName}
            Khu vực: {zone}
            Hạn xử lý: {dueAt}

            Nội dung:
            {description}

            Mở nhiệm vụ: {frontendBaseUrl}/tasks/{task.TaskId}

            Vui lòng đăng nhập PORMS để tiếp nhận và cập nhật tiến độ nhiệm vụ.
            """;
    }

    private static string TranslatePriority(string priority) => priority switch
    {
        "LOW" => "Thấp",
        "MEDIUM" => "Cần lưu ý",
        "HIGH" => "Cao",
        "CRITICAL" => "Rất cao",
        _ => priority
    };

    private static string TranslateStatus(string status) => status switch
    {
        "NEW" => "Mới",
        "ACKNOWLEDGED" => "Đã xác nhận",
        "IN_PROGRESS" => "Đang thực hiện",
        "COMPLETED" => "Đã hoàn tất",
        _ => status
    };
}
