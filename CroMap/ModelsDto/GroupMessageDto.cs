public class GroupMessageDto
{
    public string UserName { get; set; }  // ← UserName, ne name
    public string Text { get; set; }
    public DateTime Time { get; set; }   // ← DateTime, ne string
}