using SendGrid;
using SendGrid.Helpers.Mail;

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
            var apiKey = _config["SendGrid__ApiKey"] ?? _config["SendGrid:ApiKey"] ?? "";
            var fromEmail = _config["Email__From"] ?? _config["Email:From"] ?? "adminvaraapp@gmail.com";
            var fromName = _config["Email__FromName"] ?? _config["Email:FromName"] ?? "VARA";

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress(fromEmail, fromName);
            var toAddress = new EmailAddress(to);

            var msg = new SendGridMessage
            {
                From = from,
                Subject = subject,
                HtmlContent = htmlBody
            };
            msg.AddTo(toAddress);

            // Inline slike kao attachmenti s Content-ID
            foreach (var img in inlineImages)
            {
                msg.AddAttachment(new Attachment
                {
                    Content = img.Base64Data,
                    Type = img.MimeType,
                    Filename = img.FileName,
                    Disposition = "inline",
                    ContentId = img.ContentId
                });
            }

            var response = await client.SendEmailAsync(msg);

            if ((int)response.StatusCode >= 400)
            {
                var body = await response.Body.ReadAsStringAsync();
                _logger.LogError("SendGrid error {Status}: {Body}", response.StatusCode, body);
                throw new Exception($"SendGrid failed: {response.StatusCode}");
            }

            _logger.LogInformation("Email sent via SendGrid to {To}", to);
        }
    }
}