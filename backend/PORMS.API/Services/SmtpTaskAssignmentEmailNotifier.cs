using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using PORMS.API.Configuration;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Services;

public sealed class SmtpTaskAssignmentEmailNotifier : ITaskAssignmentEmailNotifier
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpTaskAssignmentEmailNotifier> _logger;

    public SmtpTaskAssignmentEmailNotifier(
        IOptions<EmailOptions> options,
        ILogger<SmtpTaskAssignmentEmailNotifier> logger)
    {
        _options = options.Value;
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
            From = new MailAddress(smtp.FromEmail, smtp.FromName),
            Subject = $"PORMS - Nhiem vu moi: {task.TaskCode}",
            Body = BuildBody(task),
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(task.AssignedUserEmail, task.AssignedUserName ?? task.AssignedUserEmail));

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

    private static string BuildBody(TaskLogReadModel task)
    {
        var dueAt = task.DueAt?.ToString("dd/MM/yyyy HH:mm zzz") ?? "Chua dat";
        var zone = string.IsNullOrWhiteSpace(task.ZoneName) ? "Chua co khu vuc" : task.ZoneName;
        var description = string.IsNullOrWhiteSpace(task.Description) ? "Khong co mo ta." : task.Description;

        return $"""
            Xin chao {task.AssignedUserName ?? task.AssignedUserEmail},

            Ban vua duoc phan cong mot nhiem vu tren he thong PORMS.

            Ma nhiem vu: {task.TaskCode}
            Tieu de: {task.Title}
            Uu tien: {task.Priority}
            Trang thai: {task.Status}
            Cang: {task.PortCode} - {task.PortName}
            Khu vuc: {zone}
            Han xu ly: {dueAt}

            Mo ta:
            {description}

            Vui long dang nhap PORMS de xac nhan va cap nhat tien do nhiem vu.
            """;
    }
}
