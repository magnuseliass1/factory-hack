using Microsoft.Azure.Cosmos;
using SharedModels;

namespace PartsOrderingAgent.Services
{
    public class CosmosDbService
    {
        private readonly CosmosClient _client;
        private readonly Database _database;

        public CosmosDbService(string endpoint, string key, string databaseName)
        {
            _client = new CosmosClient(endpoint, key);
            _database = _client.GetDatabase(databaseName);
        }

        /// <summary>
        /// Get work order from ERP system
        /// </summary>
        public async Task<WorkOrder> GetWorkOrderAsync(string workOrderId)
        {
            var container = _database.GetContainer("WorkOrders");
            try
            {
                var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                    .WithParameter("@id", workOrderId);
                
                using var iterator = container.GetItemQueryIterator<WorkOrder>(query);
                
                if (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    if (response.Count > 0)
                    {
                        return response.First();
                    }
                }
                
                throw new Exception($"Work order {workOrderId} not found");
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new Exception($"Work order {workOrderId} not found");
            }
        }

        /// <summary>
        /// Get inventory items from WMS
        /// </summary>
        public async Task<List<InventoryItem>> GetInventoryItemsAsync(List<string> partNumbers)
        {
            try
            {
                var container = _database.GetContainer("PartsInventory");
                var results = new List<InventoryItem>();

                foreach (var partNumber in partNumbers)
                {
                    var query = new QueryDefinition(
                        "SELECT * FROM c WHERE c.partNumber = @partNumber OR c.id = @partNumber"
                    ).WithParameter("@partNumber", partNumber);

                    using var iterator = container.GetItemQueryIterator<InventoryItem>(query);
                    while (iterator.HasMoreResults)
                    {
                        var response = await iterator.ReadNextAsync();
                        results.AddRange(response);
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not retrieve inventory: {ex.Message}");
                return new List<InventoryItem>();
            }
        }

        /// <summary>
        /// Get suppliers from SCM that can provide specific parts
        /// </summary>
        public async Task<List<Supplier>> GetSuppliersForPartsAsync(List<string> partNumbers)
        {
            try
            {
                var containerResponse = await _database.CreateContainerIfNotExistsAsync(
                    "Suppliers",
                    "/id"
                );
                var results = new List<Supplier>();

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE ARRAY_LENGTH(ARRAY_INTERSECT(c.parts, @partNumbers)) > 0"
                ).WithParameter("@partNumbers", partNumbers);

                using var iterator = containerResponse.Container.GetItemQueryIterator<Supplier>(query);
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                return results.Count > 0 ? results : GenerateMockSuppliers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not retrieve suppliers: {ex.Message}");
                return GenerateMockSuppliers();
            }
        }

        private List<Supplier> GenerateMockSuppliers()
        {
            return new List<Supplier>
            {
                new Supplier
                {
                    Id = "supplier-001",
                    Name = "Industrial Parts Supply Co.",
                    Parts = new List<string>(),
                    Reliability = "High",
                    LeadTimeDays = 3,
                    ContactEmail = "orders@industrialparts.com"
                },
                new Supplier
                {
                    Id = "supplier-002",
                    Name = "Quick Parts Ltd.",
                    Parts = new List<string>(),
                    Reliability = "Medium",
                    LeadTimeDays = 1,
                    ContactEmail = "sales@quickparts.com"
                }
            };
        }

        /// <summary>
        /// Save parts order to SCM
        /// </summary>
        public async Task<PartsOrder> SavePartsOrderAsync(PartsOrder order)
        {
            var containerResponse = await _database.CreateContainerIfNotExistsAsync(
                "PartsOrders",
                "/id"
            );
            var response = await containerResponse.Container.CreateItemAsync(order, new PartitionKey(order.Id));
            return response.Resource;
        }

        /// <summary>
        /// Update work order status
        /// </summary>
        public async Task UpdateWorkOrderStatusAsync(string workOrderId, string status)
        {
            var container = _database.GetContainer("WorkOrders");
            var workOrder = await GetWorkOrderAsync(workOrderId);
            var oldStatus = workOrder.Status;
            workOrder.Status = status;
            
            // Delete the old item and create a new one with the new partition key
            await container.DeleteItemAsync<WorkOrder>(workOrderId, new PartitionKey(oldStatus));
            await container.CreateItemAsync(workOrder, new PartitionKey(status));
        }
        /// <summary>
        /// Get thread ID for a work order from persistent storage
        /// </summary>
        public async Task<string?> GetWorkOrderThreadIdAsync(string workOrderId)
        {
            try
            {
                var containerResponse = await _database.CreateContainerIfNotExistsAsync(
                    "ThreadMappings",
                    "/entityId"
                );
                
                var response = await containerResponse.Container.ReadItemAsync<ThreadMapping>(
                    workOrderId,
                    new PartitionKey(workOrderId)
                );
                
                return response.Resource.ThreadId;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        /// <summary>
        /// Save thread ID for a work order to persistent storage
        /// </summary>
        public async Task SaveWorkOrderThreadIdAsync(string workOrderId, string threadId)
        {
            var containerResponse = await _database.CreateContainerIfNotExistsAsync(
                "ThreadMappings",
                "/entityId"
            );
            
            var mapping = new ThreadMapping
            {
                Id = workOrderId,
                EntityId = workOrderId,
                EntityType = "workorder",
                ThreadId = threadId,
                Purpose = "parts_ordering",
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };
            
            await containerResponse.Container.UpsertItemAsync(mapping, new PartitionKey(workOrderId));
        }

        /// <summary>
        /// Get chat history for a work order from persistent storage
        /// </summary>
        public async Task<string?> GetWorkOrderChatHistoryAsync(string workOrderId)
        {
            try
            {
                var containerResponse = await _database.CreateContainerIfNotExistsAsync(
                    "ChatHistories",
                    "/entityId"
                );
                
                var response = await containerResponse.Container.ReadItemAsync<ChatHistory>(
                    workOrderId,
                    new PartitionKey(workOrderId)
                );
                
                return response.Resource.HistoryJson;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        /// <summary>
        /// Save chat history for a work order to persistent storage
        /// </summary>
        public async Task SaveWorkOrderChatHistoryAsync(string workOrderId, string historyJson)
        {
            var containerResponse = await _database.CreateContainerIfNotExistsAsync(
                "ChatHistories",
                "/entityId"
            );
            
            var history = new ChatHistory
            {
                Id = workOrderId,
                EntityId = workOrderId,
                EntityType = "workorder",
                HistoryJson = historyJson,
                Purpose = "parts_ordering",
                UpdatedAt = DateTime.UtcNow
            };
            
            await containerResponse.Container.UpsertItemAsync(history, new PartitionKey(workOrderId));
        }
    }

    /// <summary>
    /// Thread mapping model for storing agent conversation threads
    /// </summary>
    public class ThreadMapping
    {
        public string Id { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string ThreadId { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
    }

    /// <summary>
    /// Chat history model for storing conversation history
    /// </summary>
    public class ChatHistory
    {
        public string Id { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string HistoryJson { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
