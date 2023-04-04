using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Likvido.Azure.Storage
{
    public class AzureStorageService : IAzureStorageService
    {
        private readonly BlobContainerClient _container;
        private readonly AzureSettings _azureSettings;

        public AzureStorageService(AzureSettings azureSettings, string containerName)
        {
            var blobStorage = new BlobServiceClient(azureSettings.StorageConnectionString);
            _azureSettings = azureSettings;

            _container = blobStorage.GetBlobContainerClient(containerName);
            _container.CreateIfNotExists();
            _container.SetAccessPolicy(PublicAccessType.Blob);
        }

        public async Task DeleteAsync(Uri uri)
        {
            var absolutePath = uri.AbsolutePath;
            if (absolutePath.StartsWith("/"))
            {
                absolutePath = absolutePath.Substring(1);
            }

            if (absolutePath.StartsWith(_container.Name))
            {
                absolutePath = absolutePath.Substring(_container.Name.Length + 1);
            }

            await DeleteAsync(absolutePath).ConfigureAwait(false);
        }

        public async Task DeleteAsync(string key)
        {
            var blob = _container.GetBlobClient(HttpUtility.UrlDecode(key));
            await blob.DeleteIfExistsAsync().ConfigureAwait(false);
        }

        public IEnumerable<Uri> Find(string prefix)
        {
            foreach (var blob in _container.GetBlobs(BlobTraits.None, BlobStates.None, prefix))
            {
                yield return _container.GetBlobClient(blob.Name)?.Uri;
            }
        }

        public Uri Rename(string tempFileName, string fileName)
        {
            var existingBlob = _container.GetBlobClient(tempFileName);
            if (existingBlob?.Exists() == true)
            {
                var newBlob = _container.GetBlobClient(fileName);
                var blobObj = newBlob as BlobClient;
                blobObj?.StartCopyFromUri(existingBlob.Uri);
                return newBlob.Uri;
            }
            return null;
        }

        public Uri Set(string key, Stream content, bool overwrite = true, Dictionary<string, string> metadata = null)
        {
            return Set(key, content, overwrite, 0, metadata);
        }

        private Uri Set(string key, Stream content, bool overwrite = true, int iteration = 0, Dictionary<string, string> metadata = null)
        {
            content.Seek(0, SeekOrigin.Begin);

            string duplicateAwareKey = key;
            if (!overwrite)
            {
                duplicateAwareKey = (iteration > 0) ?
                    $"{Path.GetDirectoryName(key).Replace('\\', '/')}/{Path.GetFileNameWithoutExtension(key)}({iteration.ToString()}){Path.GetExtension(key)}"
                    : key;
            }

            var blob = _container.GetBlobClient(HttpUtility.UrlDecode(duplicateAwareKey));

            try
            {
                blob.Upload(content, overwrite: overwrite);
                if (metadata != null)
                {
                    blob.SetMetadata(metadata);
                }
            }
            catch (RequestFailedException ex)
            {
                if (ex.Status == (int)System.Net.HttpStatusCode.Conflict)
                {
                    return Set(key, content, overwrite, ++iteration, metadata);
                }
            }
            return blob.CustomUri(_azureSettings.StorageAlternateUri);
        }

        public async Task<MemoryStream> GetAsync(Uri uri)
        {
            if (uri.AbsolutePath.Contains(_container.Name))
            {
                return await GetAsync(uri.AbsolutePath.Substring(uri.AbsolutePath.LastIndexOf('/') + 1)).ConfigureAwait(false);
            }
            return null;
        }

        public async Task<MemoryStream> GetAsync(string key)
        {
            try
            {
                var stream = new MemoryStream();
                var blob = new BlobClient(new Uri(key));
                await blob.DownloadToAsync(stream).ConfigureAwait(false);
                stream.Position = 0;
                return stream;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<Uri> RenameAsync(string tempFileName, string fileName)
        {
            var existingBlob = _container.GetBlobClient(tempFileName);
            if (existingBlob?.Exists())
            {
                var newBlob = _container.GetBlobClient(fileName);
                var blobObj = newBlob as BlobClient;
                if (blobObj?.Exists())
                {
                    await blobObj.StartCopyFromUriAsync(existingBlob.Uri).ConfigureAwait(false);
                }
                return newBlob.Uri;
            }
            return null;
        }

        public async Task<Uri> SetAsync(string key, Stream content, string friendlyName = null, bool overwrite = true, Dictionary<string, string> metadata = null)
        {
            return await SetAsync(key, content, friendlyName, overwrite, 0, metadata).ConfigureAwait(false);
        }

        private async Task<Uri> SetAsync(string key, Stream content, string friendlyName = null, bool overwrite = true, int iteration = 0, Dictionary<string, string> metadata = null)
        {
            content.Seek(0, SeekOrigin.Begin);
            string duplicateAwareKey = key;
            if (!overwrite)
            {
                duplicateAwareKey = (iteration > 0) ?
                    $"{Path.GetDirectoryName(key).Replace('\\', '/')}/{Path.GetFileNameWithoutExtension(key)}({iteration.ToString()}){Path.GetExtension(key)}"
                    : key;
            }

            var blob = _container.GetBlobClient(HttpUtility.UrlDecode(duplicateAwareKey));

            try
            {
                await blob.UploadAsync(content, overwrite: overwrite).ConfigureAwait(false);
                if (metadata != null)
                {
                    await blob.SetMetadataAsync(metadata).ConfigureAwait(false);
                }
            }
            catch (RequestFailedException ex)
            {
                if (ex.Status == (int)System.Net.HttpStatusCode.Conflict)
                {
                    return await SetAsync(key, content, friendlyName, overwrite, ++iteration, metadata).ConfigureAwait(false);
                }
            }

            if (!string.IsNullOrWhiteSpace(friendlyName))
            {
                // Get the existing properties
                BlobProperties properties = await blob.GetPropertiesAsync().ConfigureAwait(false);

                var headers = new BlobHttpHeaders
                {
                    ContentDisposition = $"attachment; filename={friendlyName}",
                    ContentType = "application/octet-stream",

                    // Populate remaining headers with 
                    // the pre-existing properties
                    CacheControl = properties.CacheControl,
                    ContentEncoding = properties.ContentEncoding,
                    ContentHash = properties.ContentHash
                };

                // Set the blob's properties.
                await blob.SetHttpHeadersAsync(headers);
            }

            return blob.CustomUri(_azureSettings.StorageAlternateUri);
        }

        public async Task<string> GetBlobSasUriAsync(string url)
        {
            var (accountName, accountKey) = _azureSettings.GetStorageAccountInfo();
            var blobClient = new BlobClient(new Uri(url), credential: new StorageSharedKeyCredential(accountName, accountKey));
            var exist = await blobClient.ExistsAsync().ConfigureAwait(false);
            if (!exist)
            {
                return null;
            }

            //  Defines the resource being accessed and for how long the access is allowed.
            var blobSasBuilder = new BlobSasBuilder
            {
                ExpiresOn = DateTime.UtcNow.AddMinutes(1)
            };

            //  Defines the type of permission.
            blobSasBuilder.SetPermissions(BlobSasPermissions.Read);
            var sasBlobToken = blobClient.GenerateSasUri(blobSasBuilder);
            return sasBlobToken.AbsoluteUri;
        }
    }

    public static class BlobExtensions
    {
        public static Uri CustomUri(this BlobClient x, string alternateUri)
        {
            var uri = x.Uri.ToString();
            if (!string.IsNullOrEmpty(alternateUri))
            {
                uri = uri.Replace(x.Uri.Authority, alternateUri);
            }

            var properties = x.GetProperties()?.Value;
            if (properties == null)
            {
                return new Uri(uri);
            }

            return new Uri(uri + "?t=" + properties.ETag.ToString().Trim('\"'));
        }
    }
}
