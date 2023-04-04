using System.Linq;

namespace Likvido.Azure
{
    public class AzureSettings
    {
        public string StorageConnectionString { get; set; }
        public string StorageAlternateUri { get; set; }
        public string EventGridTopic { get; set; }
        public string EventGridAccessKey { get; set; }
        public string InstrumentationKey { get; set; }

        public (string StorageAccountName, string StorageAccountKey) GetStorageAccountInfo()
        {
            var accountInfo = StorageConnectionString.Split(';').Where(x => x.Length > 0).Select(x => x.Split(new[] { '=' }, 2)).ToDictionary(x => x[0], x => x[1]);
            return (accountInfo["AccountName"].ToString(), accountInfo["AccountKey"].ToString());
        }
    }
}
