using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Notification.Infrastructure.Services;

public interface IEmailService
{
    Task SendAsync(string to, string replyTo, string subject, string body, CancellationToken ct = default);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }
    public async Task SendAsync(string to, string replyTo, string subject, string body, CancellationToken ct = default)
    {
        var systemFrom = _configuration["Email:From"];
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(systemFrom));
        message.To.Add(MailboxAddress.Parse(to));
        message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(
             _configuration["Email:Host"],
             int.Parse(_configuration["Email:Port"]!),
             SecureSocketOptions.None,
             ct);

        var username = _configuration["Email:Username"];
        if (!string.IsNullOrWhiteSpace(username))
        {
            await client.AuthenticateAsync(
                username,
                _configuration["Email:Password"],
                ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}