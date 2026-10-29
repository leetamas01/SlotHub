using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace IdopontfoglaloWebalk.Models
{
    public interface IEmailSenderService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendEmailWithAttachmentAsync(string toEmail, string subject, string message, Attachment attachment);
    }

    public class EmailSenderService : IEmailSenderService
    {
        private readonly IConfiguration _config;

        public EmailSenderService(IConfiguration config)
        {
            _config = config;
        }

        public Task SendEmailAsync(string toEmail, string subject, string message)
        {
            return SendEmailWithAttachmentAsync(toEmail, subject, message, null);
        }
        public async Task SendEmailWithAttachmentAsync(string toEmail, string subject, string message, Attachment attachment)
        {
            var server = _config["SmtpSettings:Server"];
            var port = int.Parse(_config["SmtpSettings:Port"] ?? "587");
            var senderName = _config["SmtpSettings:SenderName"];
            var senderEmail = _config["SmtpSettings:SenderEmail"];
            var password = _config["SmtpSettings:Password"];

            using var client = new SmtpClient(server, port)
            {
                Credentials = new NetworkCredential(senderEmail, password),
                EnableSsl = true
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = message,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            if (attachment != null)
            {
                mailMessage.Attachments.Add(attachment);
            }

            await client.SendMailAsync(mailMessage);
        }
    }
}