using System.Net;
using System.Net.Mail;
using System.Net.Mime;

namespace CroMap.Services
{
    public class EmailService : IEmailServiceWithInlineImages
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            await SendEmailWithInlineImagesAsync(to, subject, htmlBody, new List<InlineImageAttachment>());
        }

        public async Task SendEmailWithInlineImagesAsync(
            string to, string subject, string htmlBody,
            List<InlineImageAttachment> inlineImages)
        {
            var smtpHost = _config["Email:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var smtpUser = _config["Email:Username"] ?? "";
            var smtpPass = _config["Email:Password"] ?? "";
            var fromEmail = _config["Email:From"] ?? smtpUser;
            var fromName = _config["Email:FromName"] ?? "VARA";

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(smtpUser, smtpPass)
            };

            using var message = new MailMessage();
            message.From = new MailAddress(fromEmail, fromName);
            message.To.Add(to);
            message.Subject = subject;

            if (inlineImages.Count == 0)
            {
                message.IsBodyHtml = true;
                message.Body = htmlBody;
            }
            else
            {
                // multipart/related - CID inline slike
                // Gmail prikazuje slike jer su dio emaila (ne eksterni URL)
                var htmlView = AlternateView.CreateAlternateViewFromString(
                    htmlBody, null, MediaTypeNames.Text.Html);

                foreach (var img in inlineImages)
                {
                    var imageBytes = Convert.FromBase64String(img.Base64Data);
                    var imageStream = new MemoryStream(imageBytes);
                    var linkedResource = new LinkedResource(imageStream, img.MimeType)
                    {
                        ContentId = img.ContentId,
                        TransferEncoding = TransferEncoding.Base64
                    };
                    htmlView.LinkedResources.Add(linkedResource);
                }

                message.AlternateViews.Add(htmlView);
            }

            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent to {To} with subject: {Subject}", to, subject);
        }
    }
}