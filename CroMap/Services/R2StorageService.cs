using Amazon.S3;
using Amazon.S3.Model;

namespace CroMap.Services
{
    public interface IR2StorageService
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder);
        Task DeleteFileAsync(string fileUrl);
    }

    public class R2StorageService : IR2StorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _publicUrl;

        public R2StorageService(IConfiguration configuration)
        {
            var accessKey = configuration["R2:AccessKeyId"];
            var secretKey = configuration["R2:SecretAccessKey"];
            var endpoint = configuration["R2:Endpoint"];
            _bucketName = configuration["R2:BucketName"];
            _publicUrl = configuration["R2:PublicUrl"];

            var config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true
            };

            _s3Client = new AmazonS3Client(accessKey, secretKey, config);
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder)
        {
            var key = $"{folder}/{fileName}";

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = fileStream,
                ContentType = contentType,
                DisablePayloadSigning = true // R2 zahtijeva ovo za neke regije
            };

            await _s3Client.PutObjectAsync(request);

            // Vrati javni URL
            return $"{_publicUrl.TrimEnd('/')}/{key}";
        }

        public async Task DeleteFileAsync(string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                return;

            // Izvuci "key" iz punog URL-a (sve nakon javnog URL prefiksa)
            var key = fileUrl.Replace(_publicUrl.TrimEnd('/') + "/", "");

            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            try
            {
                await _s3Client.DeleteObjectAsync(request);
            }
            catch
            {
                // Ignoriraj greške pri brisanju (npr. fajl već ne postoji)
            }
        }
    }
}