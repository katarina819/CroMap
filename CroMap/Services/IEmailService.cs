namespace CroMap.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string htmlBody);
    }

    public interface IEmailServiceWithInlineImages : IEmailService
    {
        Task SendEmailWithInlineImagesAsync(
            string to,
            string subject,
            string htmlBody,
            List<InlineImageAttachment> inlineImages);
    }

    // JEDNA klasa, u Services namespaceu - koristi je i AuthController i EmailService
    public class InlineImageAttachment
    {
        public string ContentId { get; set; } = "";
        public string Base64Data { get; set; } = "";
        public string MimeType { get; set; } = "image/png";
        public string FileName { get; set; } = "";
    }
}