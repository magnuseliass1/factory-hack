using Microsoft.Azure.Cosmos;
using SharedModels;

namespace PredictiveMaintenanceAgent.Services
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
        /// Get historical maintenance records for a machine
        /// </summary>
        public async Task<List<MaintenanceHistory>> GetMaintenanceHistoryAsync(string machineId)
        {
            try
            {
                // Try to get or create the container
                var container = await _database.CreateContainerIfNotExistsAsync(
                    "MaintenanceHistory",
                    "/machineId"
                );
                
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.machineId = @machineId ORDER BY c.occurrenceDate DESC"
                ).WithParameter("@machineId", machineId);

                var results = new List<MaintenanceHistory>();
                using var iterator = container.Container.GetItemQueryIterator<MaintenanceHistory>(query);
                
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not retrieve maintenance history: {ex.Message}");
                return new List<MaintenanceHistory>();
            }
        }

        /// <summary>
        /// Get available maintenance windows from MES
        /// </summary>
        public async Task<List<MaintenanceWindow>> GetAvailableMaintenanceWindowsAsync(int daysAhead = 14)
        {
            try
            {
                var container = await _database.CreateContainerIfNotExistsAsync(
                    "MaintenanceWindows",
                    "/id"
                );
                
                var startDate = DateTime.UtcNow;
                var endDate = startDate.AddDays(daysAhead);

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.startTime >= @startDate AND c.startTime <= @endDate AND c.isAvailable = true ORDER BY c.startTime"
                ).WithParameter("@startDate", startDate)
                 .WithParameter("@endDate", endDate);

                var results = new List<MaintenanceWindow>();
                using var iterator = container.Container.GetItemQueryIterator<MaintenanceWindow>(query);
                
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                return results.Count > 0 ? results : GenerateMockMaintenanceWindows(daysAhead);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not retrieve maintenance windows: {ex.Message}");
                return GenerateMockMaintenanceWindows(daysAhead);
            }
        }

        private List<MaintenanceWindow> GenerateMockMaintenanceWindows(int daysAhead)
        {
            var windows = new List<MaintenanceWindow>();
            var startDate = DateTime.UtcNow.Date.AddDays(1);
            
            for (int i = 0; i < daysAhead; i++)
            {
                windows.Add(new MaintenanceWindow
                {
                    Id = $"mw-{startDate.AddDays(i):yyyy-MM-dd}-night",
                    StartTime = startDate.AddDays(i).AddHours(22),
                    EndTime = startDate.AddDays(i).AddHours(30),
                    IsAvailable = true,
                    ProductionImpact = "Low"
                });
            }
            
            return windows;
        }

        /// <summary>
        /// Save maintenance schedule to database
        /// </summary>
        public async Task<MaintenanceSchedule> SaveMaintenanceScheduleAsync(MaintenanceSchedule schedule)
        {
            var containerResponse = await _database.CreateContainerIfNotExistsAsync(
                "MaintenanceSchedules",
                "/id"
            );
            var response = await containerResponse.Container.CreateItemAsync(schedule, new PartitionKey(schedule.Id));
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
        /// Get thread ID for a machine from persistent storage
        /// </summary>
        public async Task<string?> GetMachineThreadIdAsync(string machineId)
        {
            try
            {
                var containerResponse = await _database.CreateContainerIfNotExistsAsync(
                    "ThreadMappings",
                    "/entityId"
                );
                
                var response = await containerResponse.Container.ReadItemAsync<ThreadMapping>(
                    machineId,
                    new PartitionKey(machineId)
                );
                
                return response.Resource.ThreadId;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        /// <summary>
        /// Save thread ID for a machine to persistent storage
        /// </summary>
        public async Task SaveMachineThreadIdAsync(string machineId, string threadId)
        {
            var containerResponse = await _database.CreateContainerIfNotExistsAsync(
                "ThreadMappings",
                "/entityId"
            );
            
            var mapping = new ThreadMapping
            {
                Id = machineId,
                EntityId = machineId,
                EntityType = "machine",
                ThreadId = threadId,
                Purpose = "predictive_maintenance",
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };
            
            await containerResponse.Container.UpsertItemAsync(mapping, new PartitionKey(machineId));
        }

        /// <summary>
        /// Get chat history for a machine from persistent storage
        /// </summary>
        public async Task<string?> GetMachineChatHistoryAsync(string machineId)
        {
            try
            {
                var containerResponse = await _database.CreateContainerIfNotExistsAsync(
                    "ChatHistories",
                    "/entityId"
                );
                
                var response = await containerResponse.Container.ReadItemAsync<ChatHistory>(
                    machineId,
                    new PartitionKey(machineId)
                );
                
                return response.Resource.HistoryJson;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        /// <summary>
        /// Save chat history for a machine to persistent storage
        /// </summary>
        public async Task SaveMachineChatHistoryAsync(string machineId, string historyJson)
        {
            var containerResponse = await _database.CreateContainerIfNotExistsAsync(
                "ChatHistories",
                "/entityId"
            );
            
            var history = new ChatHistory
            {
                Id = machineId,
                EntityId = machineId,
                EntityType = "machine",
                HistoryJson = historyJson,
                Purpose = "predictive_maintenance",
                UpdatedAt = DateTime.UtcNow
            };
            
            await containerResponse.Container.UpsertItemAsync(history, new PartitionKey(machineId));
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
