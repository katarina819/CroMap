namespace CroMap.ModelsDto
{
    public class VideoUploadRequest
    {
        public IFormFile Video { get; set; }
        public string Title { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public int UserId { get; set; }
        public string MediaType { get; set; } = "video";
    }
}