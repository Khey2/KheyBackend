using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string bodyHtml);
}

namespace KheyBackend.Services
{
    public class EmailService: IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string bodyHtml)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(smtpSettings["SenderName"], smtpSettings["SenderEmail"] ));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;
            email.Body = new TextPart(TextFormat.Html) { Text = bodyHtml };



            using var smtp = new SmtpClient();

            // Conexión asíncrona a Mailtrap
            await smtp.ConnectAsync(
                smtpSettings["Server"],
                int.Parse(smtpSettings["Port"]!),
                SecureSocketOptions.StartTls
            );

            await smtp.AuthenticateAsync(
                smtpSettings["Username"],
                smtpSettings["Password"]
            );

            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }
}
