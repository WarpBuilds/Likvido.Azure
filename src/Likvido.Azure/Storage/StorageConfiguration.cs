using System.Linq;

namespace Likvido.Azure.Storage
{
    public class StorageConfiguration
    {
        public string ConnectionString { get; set; }

        public (string StorageAccountName, string StorageAccountKey) GetStorageAccountInfo()
        {
            var accountInfo = ConnectionString.Split(';').Where(x => x.Length > 0).Select(x => x.Split(new[] { '=' }, 2)).ToDictionary(x => x[0], x => x[1]);

            return (accountInfo["AccountName"], accountInfo["AccountKey"]);
        }
    }
}
