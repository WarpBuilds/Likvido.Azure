namespace Likvido.Azure.Storage
{
    public interface IAzureStorageServiceFactory
    {
        IAzureStorageService Create(string containerName);
    }
}
