using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens.Experimental;
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
    //public async Task SendEmail(EmailDTO emailDTO)
    //{
    //    MimeMessage message = new();

    //    message.From.Add(new MailboxAddress("My Ecom", configuration["EmailSetting:From"]));
       
    //    message.Subject = emailDTO.Subject;
        
    //    message.To.Add(new MailboxAddress(emailDTO.To, emailDTO.To));
        
    //    message.Body = new TextPart(MimeKit.Text.TextFormat.Html)
    //    {
    //        Text = emailDTO.Content
    //    };
    //    using (var smtp = new MailKit.Net.Smtp.SmtpClient())
    //    {
    //        try
    //        {
    //            await smtp.ConnectAsync(
    //                configuration["EmailSetting:Smtp"],
    //               int.Parse(configuration["EmailSetting:Port"]), true);

    //            await smtp.AuthenticateAsync(configuration["EmailSetting:Username"],
    //                configuration["EmailSetting:Password"]);

    //            await smtp.SendAsync(message);
    //        }
    //        catch (Exception ex)
    //        {

    //            throw;
    //        }
    //        finally
    //        {
    //            smtp.Disconnect(true);
    //            smtp.Dispose();
    //        }
    //    }
    //}
    /*
     
    public class MessageEmail
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }

    }
     */
    public async Task SendEmail(EmailDTO msg)
    {
        try
        {
            string fromMail = configuration["EmailSetting:From"];
            string fromPassword = configuration["EmailSetting:Password"];

            MailMessage message = new MailMessage();

            message.From = new MailAddress(fromMail);
            message.Subject = msg.Subject;
            message.To.Add(new MailAddress(msg.To));
            message.Body = $"<html><body> {msg.Content} </body></html>";
            message.IsBodyHtml = true;

            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(fromMail, fromPassword),
                EnableSsl = true
            };
            smtpClient.Send(message);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error sending email: {ex.Message}");
        }
    }
}