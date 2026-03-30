namespace CroMap.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string Content { get; set; }
        public bool IsRead { get; set; }
        public DateTime SentAt { get; set; }

        // Navigacijska polja (ne mapiraju se u bazu)
        public string SenderName { get; set; }
        public string ReceiverName { get; set; }
    }
}