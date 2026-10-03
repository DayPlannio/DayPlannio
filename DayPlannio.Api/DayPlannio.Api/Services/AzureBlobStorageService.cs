using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace DayPlannio.Api.Services
{
    public class AzureBlobStorageService
    {
        private readonly BlobContainerClient _container;
        private readonly StorageSharedKeyCredential _keyCredential;

        public AzureBlobStorageService(IConfiguration configuration)
        {
            var connectionString = configuration["AzureBlobStorage:ConnectionString"] ?? "";
            var containerName = configuration["AzureBlobStorage:ContainerName"] ?? "fotos";

            _container = new BlobContainerClient(connectionString, containerName);
            _container.CreateIfNotExists(PublicAccessType.None);

            var serviceClient = new BlobServiceClient(connectionString);
            _keyCredential = new StorageSharedKeyCredential(
                serviceClient.AccountName,
                ParseAccountKey(connectionString));
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName)
        {
            var blobName = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}_{fileName}";
            var blobClient = _container.GetBlobClient(blobName);
            await blobClient.UploadAsync(fileStream, overwrite: true);
            return blobClient.Uri.ToString();
        }

        public string GerarUrlPublica(string blobUri)
        {
            var cleanUri = blobUri.Split('?')[0];
            var blobClient = new BlobClient(new Uri(cleanUri));

            if (!_container.CanGenerateSasUri)
                return cleanUri;

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _container.Name,
                BlobName = blobClient.Name,
                Resource = "b",
                ExpiresOn = new DateTimeOffset(new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc))
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasToken = sasBuilder.ToSasQueryParameters(_keyCredential).ToString();
            return $"{cleanUri}?{sasToken}";
        }

        public async Task DeleteAsync(string blobUri)
        {
            var cleanUri = blobUri.Split('?')[0];
            var blobName = new Uri(cleanUri).AbsolutePath.TrimStart('/');
            var blobClient = _container.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }

        private static string ParseAccountKey(string connectionString)
        {
            foreach (var part in connectionString.Split(';'))
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith("AccountKey=", StringComparison.OrdinalIgnoreCase))
                    return trimmed.Substring("AccountKey=".Length);
            }
            throw new InvalidOperationException("AccountKey not found in connection string");
        }
    }
}
