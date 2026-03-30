namespace CroMap.ModelsDto
{
    public class VideoUploadRequest
    {
        public string Title { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }
        public int UserId { get; set; }
        public IFormFile Video { get; set; }
    }
}