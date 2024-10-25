using Core.DTO.General;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Net.Mail;

namespace AuthenticationApi.Services
{
    public interface IMailer
    {
        Task SendEmailAsync(string email, string subject, string body);
    }
    public class MailerService: IMailer
    {
        private readonly StmpSettings _stmpSettings;
        private readonly IWebHostEnvironment _env;
        public MailerService(IOptions<StmpSettings> stmpSettings, IWebHostEnvironment env)
        {
            _stmpSettings = stmpSettings.Value;
            _env = env;
        }
        public async Task SendEmailAsync(string email, string subject, string body)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_stmpSettings.SenderName, _stmpSettings.SenderEmail));
                message.To.Add(new MailboxAddress("User", email));
                message.Subject = subject;
                message.Body = new TextPart("html")
                {
                    Text = body
                };
                using (var client = new MailKit.Net.Smtp.SmtpClient())
                {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    await client.ConnectAsync(_stmpSettings.Server, _stmpSettings.Port, false);
                    await client.AuthenticateAsync(_stmpSettings.Username, _stmpSettings.Password);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(ex.Message);
            }
        }

    }
}
