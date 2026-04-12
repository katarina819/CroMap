namespace CroMap.Models
{
    public class UpdateSettingsRequest
    {
        public bool IsPublic { get; set; }

        public bool ShowUsername { get; set; }

        public int ScreenTimeLimitMinutes { get; set; }
    }
}