// Models/GoldenFriend.cs
namespace CroMap.Models
{
    public class GoldenFriend
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Avatar { get; set; }
    }
}