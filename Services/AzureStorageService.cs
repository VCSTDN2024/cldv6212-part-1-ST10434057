
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using System.Text;
using ABC.Retail.StorageApp.Models;

namespace ABC.Retail.StorageApp.Services
{
    public class AzureStorageService
    {
        private readonly IConfiguration _config;

        public string ConnectionString { get; }
        public string CustomersTableName { get; }
        public string ProductsTableName { get; }
        public string BlobContainerName { get; }
        public string QueueName { get; }
        public string FileShareName { get; }

        public AzureStorageService(IConfiguration config)
        {
            _config = config;
            ConnectionString = _config["AzureStorage:ConnectionString"] ?? string.Empty;
            CustomersTableName = _config["AzureStorage:TableNames:Customers"] ?? "Customers";
            ProductsTableName = _config["AzureStorage:TableNames:Products"] ?? "Products";
            BlobContainerName = _config["AzureStorage:BlobContainer"] ?? "product-images";
            QueueName = _config["AzureStorage:QueueName"] ?? "retail-events";
            FileShareName = _config["AzureStorage:FileShare"] ?? "contracts";
        }

        public async Task EnsureResourcesAsync()
        {
            // Tables
            var tableService = new TableServiceClient(ConnectionString);
            await tableService.CreateTableIfNotExistsAsync(CustomersTableName);
            await tableService.CreateTableIfNotExistsAsync(ProductsTableName);

            // Blob Container
            var blobService = new BlobServiceClient(ConnectionString);
            await blobService.CreateBlobContainerAsync(BlobContainerName, PublicAccessType.Blob).ConfigureAwait(false);
            try { await blobService.GetBlobContainerClient(BlobContainerName).SetAccessPolicyAsync(PublicAccessType.Blob); } catch { }

            // Queue
            var queueService = new QueueServiceClient(ConnectionString);
            await queueService.CreateQueueAsync(QueueName);

            // File Share
            var fileService = new ShareServiceClient(ConnectionString);
            await fileService.CreateShareAsync(FileShareName);
        }

        // TABLES
        public async Task<List<CustomerEntity>> GetCustomersAsync()
        {
            var client = new TableClient(ConnectionString, CustomersTableName);
            var list = new List<CustomerEntity>();
            await foreach (var e in client.QueryAsync<CustomerEntity>(x => x.PartitionKey == "Customer"))
            {
                list.Add(e);
            }
            return list;
        }

        public async Task AddCustomerAsync(CustomerEntity entity)
        {
            var client = new TableClient(ConnectionString, CustomersTableName);
            entity.PartitionKey = "Customer";
            entity.RowKey = Guid.NewGuid().ToString("N");
            await client.AddEntityAsync(entity);
        }

        public async Task<List<ProductEntity>> GetProductsAsync()
        {
            var client = new TableClient(ConnectionString, ProductsTableName);
            var list = new List<ProductEntity>();
            await foreach (var e in client.QueryAsync<ProductEntity>(x => x.PartitionKey == "Product"))
            {
                list.Add(e);
            }
            return list;
        }

        public async Task AddProductAsync(ProductEntity entity)
        {
            var client = new TableClient(ConnectionString, ProductsTableName);
            entity.PartitionKey = "Product";
            entity.RowKey = Guid.NewGuid().ToString("N");
            await client.AddEntityAsync(entity);
        }

        // BLOBS
        public async Task<string> UploadImageAsync(string fileName, Stream content, string contentType)
        {
            var container = new BlobContainerClient(ConnectionString, BlobContainerName);
            await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
            var blob = container.GetBlobClient(fileName);
            await blob.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType });
            return blob.Uri.ToString();
        }

        public async Task<List<string>> ListImageUrlsAsync()
        {
            var container = new BlobContainerClient(ConnectionString, BlobContainerName);
            var urls = new List<string>();
            await foreach (var item in container.GetBlobsAsync())
            {
                urls.Add(container.GetBlobClient(item.Name).Uri.ToString());
            }
            return urls;
        }

        // QUEUES
        public async Task EnqueueMessageAsync(string messageText)
        {
            var queue = new QueueClient(ConnectionString, QueueName);
            await queue.CreateIfNotExistsAsync();
            await queue.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(messageText)));
        }

        public async Task<List<string>> PeekMessagesAsync(int maxMessages = 10)
        {
            var queue = new QueueClient(ConnectionString, QueueName);
            await queue.CreateIfNotExistsAsync();
            var msgs = await queue.PeekMessagesAsync(maxMessages);
            var list = new List<string>();
            foreach (var m in msgs.Value)
            {
                try
                {
                    var text = Encoding.UTF8.GetString(Convert.FromBase64String(m.MessageText));
                    list.Add(text);
                }
                catch
                {
                    list.Add(m.MessageText ?? string.Empty);
                }
            }
            return list;
        }

        public async Task<int> DequeueAndDeleteAsync(int count = 1)
        {
            var queue = new QueueClient(ConnectionString, QueueName);
            await queue.CreateIfNotExistsAsync();
            int removed = 0;
            var msgs = await queue.ReceiveMessagesAsync(count);
            foreach (var m in msgs.Value)
            {
                await queue.DeleteMessageAsync(m.MessageId, m.PopReceipt);
                removed++;
            }
            return removed;
        }

        // FILES (Azure Files)
        public async Task UploadContractAsync(string fileName, Stream content)
        {
            var share = new ShareClient(ConnectionString, FileShareName);
            await share.CreateIfNotExistsAsync();
            var root = share.GetRootDirectoryClient();
            await root.CreateIfNotExistsAsync();
            var file = root.GetFileClient(fileName);
            await file.CreateAsync(content.Length);
            await file.UploadAsync(content);
        }

        public async Task<List<string>> ListContractsAsync()
        {
            var share = new ShareClient(ConnectionString, FileShareName);
            var root = share.GetRootDirectoryClient();
            var list = new List<string>();
            await foreach (var item in root.GetFilesAndDirectoriesAsync())
            {
                if (!item.IsDirectory)
                {
                    list.Add(item.Name);
                }
            }
            return list;
        }

        public async Task<Stream> DownloadContractAsync(string fileName)
        {
            var share = new ShareClient(ConnectionString, FileShareName);
            var root = share.GetRootDirectoryClient();
            var file = root.GetFileClient(fileName);
            var download = await file.DownloadAsync();
            var ms = new MemoryStream();
            await download.Value.Content.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }
    }

    // Hosted service to ensure resources and seed 5 records/messages/files
    public class StartupSeeder : IHostedService
    {
        private readonly AzureStorageService _svc;
        private readonly ILogger<StartupSeeder> _logger;

        public StartupSeeder(AzureStorageService svc, ILogger<StartupSeeder> logger)
        {
            _svc = svc;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _svc.EnsureResourcesAsync();

                // Seed tables if empty
                var customers = await _svc.GetCustomersAsync();
                if (customers.Count < 5)
                {
                    for (int i = 1; i <= 5; i++)
                    {
                        await _svc.AddCustomerAsync(new CustomerEntity
                        {
                            FirstName = $"Customer{i}",
                            LastName = "Demo",
                            Email = $"customer{i}@demo.local",
                            Phone = $"010-000-000{i}"
                        });
                    }
                }

                var products = await _svc.GetProductsAsync();
                if (products.Count < 5)
                {
                    for (int i = 1; i <= 5; i++)
                    {
                        await _svc.AddProductAsync(new ProductEntity
                        {
                            Name = $"Product {i}",
                            Sku = $"SKU-00{i}",
                            Price = 99.99m + i,
                            Stock = 10 * i
                        });
                    }
                }

                // Seed queue messages to 5
                var currentMsgs = await _svc.PeekMessagesAsync(32);
                if (currentMsgs.Count < 5)
                {
                    for (int i = 1; i <= 5; i++)
                    {
                        await _svc.EnqueueMessageAsync($"Processing order #{1000 + i}");
                    }
                }

                var files = await _svc.ListContractsAsync();
                if (files.Count < 5)
                {
                    for (int i = 1; i <= 5; i++)
                    {
                        var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"Contract {i} for demo purposes. Date: {DateTime.UtcNow:u}"));
                        await _svc.UploadContractAsync($"Contract_{i}.txt", content);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Startup seeding failed");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
