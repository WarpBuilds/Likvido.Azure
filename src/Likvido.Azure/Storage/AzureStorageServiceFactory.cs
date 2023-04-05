using System.Collections.Concurrent;

namespace Likvido.Azure.Storage
{
    public class AzureStorageServiceFactory : IAzureStorageServiceFactory
    {
        private readonly ConcurrentDictionary<string, IAzureStorageService> storageServices = new ConcurrentDictionary<string, IAzureStorageService>();
        private readonly StorageConfiguration storageConfiguration;

        public AzureStorageServiceFactory(StorageConfiguration storageConfiguration)
        {
            this.storageConfiguration = storageConfiguration;
        }

        public IAzureStorageService Create(string containerName)
        {
            return storageServices.GetOrAdd(containerName, name => new AzureStorageService(storageConfiguration, containerName));
        }
    }
}
