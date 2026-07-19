using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net;
using System.Net.Mail;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string message)
    {
        var emailSettings = _configuration.GetSection("EmailSettings");
        var smtpServer = emailSettings["SmtpServer"];
        var senderEmail = emailSettings["SenderEmail"];
        var appPassword = emailSettings["AppPassword"];

        if (string.IsNullOrWhiteSpace(smtpServer) ||
            string.IsNullOrWhiteSpace(senderEmail) ||
            string.IsNullOrWhiteSpace(appPassword))
        {
            throw new InvalidOperationException("SMTP configuration is incomplete.");
        }

        if (!int.TryParse(emailSettings["Port"], out var port))
        {
            port = 587;
        }

        using var client = new SmtpClient(smtpServer, port)
        {
            EnableSsl = emailSettings.GetValue("EnableSsl", true),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(senderEmail, appPassword)
        };

        using var mailMessage = new MailMessage(senderEmail, email, subject, message)
        {
            IsBodyHtml = true
        };

        try
        {
            await client.SendMailAsync(mailMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email delivery failed.");
            throw;
        }
    }
}
