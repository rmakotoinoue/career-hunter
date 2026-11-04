using CareerHunter.Application.Abstractions;
using CareerHunter.Infrastructure.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace CareerHunter.Infrastructure.Adapters;

/// <summary>
/// SMTP implementation with a logging fallback. The fallback is intentional for local runs:
/// it proves the digest pipeline end-to-end without pretending an e-mail was delivered.
/// </summary>
public sealed class EmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(EmailOptions options, ILogger<EmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public string Provider => string.IsNullOrWhiteSpace(_options.Host) ? "log" : "smtp";

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.To.Count == 0)
        {
            throw new ArgumentException("At least one recipient is required.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            _logger.LogInformation(
                "E-mail not sent (logging provider). Recipients: {RecipientCount}. Subject: {Subject}",
                message.To.Count,
                message.Subject);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            throw new InvalidOperationException("Email:FromAddress must be configured when Email:Host is set.");
        }

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));

        foreach (var recipient in message.To)
        {
            mimeMessage.To.Add(MailboxAddress.Parse(recipient));
        }

        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody
        }.ToMessageBody();

        using var client = new SmtpClient
        {
            Timeout = (int)TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds)).TotalMilliseconds
        };

        // StartTls (not StartTlsWhenAvailable): when the operator asks for TLS the credentials
        // must never fall back to a plaintext session because the server did not advertise it.
        // Local development against a TLS-less server sets Email:UseStartTls=false explicitly.
        var security = _options.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.Auto;

        await client.ConnectAsync(_options.Host, _options.Port, security, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mimeMessage, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        _logger.LogInformation(
            "E-mail sent through SMTP to {RecipientCount} recipient(s). Subject: {Subject}",
            message.To.Count,
            message.Subject);
    }
}
