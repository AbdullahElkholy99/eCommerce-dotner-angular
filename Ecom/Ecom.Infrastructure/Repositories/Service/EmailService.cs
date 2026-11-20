using Microsoft.Extensions.Configuration;
using MimeKit;
using System.Net;
using System.Net.Mail;

namespace Ecom.Infrastructure.Repositories.Service;
public class EmailService : IEmailService
{
    private readonly IConfiguration configuration;

    public EmailService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public Task SendEmail(EmailDTO msg)
    {
        try
        {
            string fromMail = configuration["EmailSetting:From"];
            string fromPassword = configuration["EmailSetting:Password"];
            string smtpHost = configuration["EmailSetting:Smtp"];
            int smtpPort = int.Parse(configuration["EmailSetting:Port"] ?? "587");

            MailMessage message = new MailMessage();

            message.From = new MailAddress(fromMail);
            message.Subject = msg.Subject;
            message.To.Add(new MailAddress(msg.To));
            message.Body = $"<html><body> {msg.Content} </body></html>";
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(smtpHost)
            {
                Port = smtpPort,
                Credentials = new NetworkCredential(fromMail, fromPassword),
                EnableSsl = true
            };
            smtpClient.Send(message);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error sending email: {ex.Message}");
        }
    }
}