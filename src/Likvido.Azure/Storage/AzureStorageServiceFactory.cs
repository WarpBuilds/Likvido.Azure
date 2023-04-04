using System.Collections.Concurrent;

namespace Likvido.Azure.Storage
{
    public class AzureStorageServiceFactory : IAzureStorageServiceFactory
    {
        private readonly ConcurrentDictionary<string, IAzureStorageService> _storageServices = new ConcurrentDictionary<string, IAzureStorageService>();

        private readonly AzureSettings _azureSettings;

        public AzureStorageServiceFactory(AzureSettings azureSettings)
        {
            _azureSettings = azureSettings;
        }

        public IAzureStorageService Create(string containerName)
        {
            return _storageServices.GetOrAdd(containerName, name => new AzureStorageService(_azureSettings, containerName));
        }
    }
}
