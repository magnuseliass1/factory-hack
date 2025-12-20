using Azure.AI.OpenAI;
using Azure.Identity;
using SharedModels;
using Newtonsoft.Json;
using System.Text;
using OpenAI.Chat;

namespace PartsOrderingAgent.Services
{
    public class PartsOrderingAgentService
    {
        private readonly string _projectEndpoint;
        private readonly string _agentId;
        private readonly CosmosDbService _cosmosService;

        public PartsOrderingAgentService(string projectEndpoint, string agentId, CosmosDbService cosmosService)
        {
            _projectEndpoint = projectEndpoint;
            _agentId = agentId;
            _cosmosService = cosmosService;
        }

        /// <summary>
        /// Generate optimized parts order using AI with persistent memory
        /// Each work order gets its own conversation thread for supplier performance tracking
        /// </summary>
        public async Task<PartsOrder> GeneratePartsOrderAsync(
            WorkOrder workOrder,
            List<InventoryItem> inventory,
            List<Supplier> suppliers)
        {
            // Build context for the AI agent
            var context = BuildOrderingContext(workOrder, inventory, suppliers);

            // Get or create chat history for this work order
            var chatHistory = await GetOrCreateWorkOrderChatHistoryAsync(workOrder.Id);
            Console.WriteLine($"   Using chat history with {chatHistory.Count} messages for work order: {workOrder.Id}");

            // Create agent client
            var endpoint = new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? throw new Exception("AZURE_OPENAI_ENDPOINT not set"));
            var deploymentName = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_DEPLOYMENT_NAME") ?? "gpt-4o";
            
            var client = new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
            var chatClient = client.GetChatClient(deploymentName);

            // Agent instructions
            var instructions = @"You are a parts ordering specialist for industrial tire manufacturing equipment. Your role is to analyze inventory status and optimize parts ordering from suppliers.

When processing parts orders:
1. Review current inventory levels for required parts
2. Check against minimum stock and reorder points
3. Identify suppliers for needed parts considering:
   - Supplier reliability and delivery performance
   - Lead time (prefer shorter)
   - Cost (balance with reliability)
   - Previous order history
4. Determine order urgency based on current stock vs. reorder point
5. Calculate expected delivery dates

Always respond in valid JSON format as requested.";

            // Build complete message list: system + history + new request
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(instructions)
            };
            
            // Add chat history (previous conversations for this work order)
            messages.AddRange(chatHistory);
            
            // Add current request
            messages.Add(new UserChatMessage(context));

            // Call the model
            var completion = await chatClient.CompleteChatAsync(messages);
            var responseText = completion.Value.Content[0].Text;

            // Store the conversation in chat history
            chatHistory.Add(new UserChatMessage(context));
            chatHistory.Add(new AssistantChatMessage(responseText));
            await SaveWorkOrderChatHistoryAsync(workOrder.Id, chatHistory);

            var jsonResponse = ExtractJsonFromResponse(responseText);
            
            // Parse the agent's ordering decision
            dynamic orderData = JsonConvert.DeserializeObject(jsonResponse) 
                ?? throw new Exception("Failed to parse order data from agent response");

            var order = new PartsOrder
            {
                Id = $"PO-{Guid.NewGuid().ToString().Substring(0, 8)}",
                WorkOrderId = workOrder.Id,
                OrderItems = ((IEnumerable<dynamic>)orderData.orderItems).Select(item => new OrderItem
                {
                    PartNumber = (string)item.partNumber,
                    PartName = (string)item.partName,
                    Quantity = (int)item.quantity,
                    UnitCost = (decimal)item.unitCost,
                    TotalCost = (decimal)item.totalCost
                }).ToList(),
                SupplierId = (string)orderData.supplierId,
                SupplierName = (string)orderData.supplierName,
                TotalCost = (decimal)orderData.totalCost,
                ExpectedDeliveryDate = DateTime.Parse((string)orderData.expectedDeliveryDate),
                OrderStatus = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            return order;
        }
        
        /// <summary>
        /// Get existing chat history for a work order from Cosmos DB
        /// </summary>
        private async Task<List<ChatMessage>> GetOrCreateWorkOrderChatHistoryAsync(string workOrderId)
        {
            var historyJson = await _cosmosService.GetWorkOrderChatHistoryAsync(workOrderId);
            
            if (string.IsNullOrEmpty(historyJson))
            {
                return new List<ChatMessage>();
            }

            try
            {
                var history = JsonConvert.DeserializeObject<List<ChatMessageDto>>(historyJson) ?? new List<ChatMessageDto>();
                return history.Select(dto => dto.Role == "user" 
                    ? (ChatMessage)new UserChatMessage(dto.Content)
                    : (ChatMessage)new AssistantChatMessage(dto.Content)).ToList();
            }
            catch
            {
                return new List<ChatMessage>();
            }
        }

        /// <summary>
        /// Save chat history for a work order to Cosmos DB
        /// </summary>
        private async Task SaveWorkOrderChatHistoryAsync(string workOrderId, List<ChatMessage> chatHistory)
        {
            // Convert to serializable format (keep last 10 messages to manage token count)
            var historyToSave = chatHistory
                .TakeLast(10)
                .Select(msg => new ChatMessageDto
                {
                    Role = msg is UserChatMessage ? "user" : "assistant",
                    Content = msg is UserChatMessage userMsg ? userMsg.Content[0].Text : ((AssistantChatMessage)msg).Content[0].Text
                }).ToList();

            var historyJson = JsonConvert.SerializeObject(historyToSave);
            await _cosmosService.SaveWorkOrderChatHistoryAsync(workOrderId, historyJson);
        }

        private class ChatMessageDto
        {
            public string Role { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }
        
        private string ExtractJsonFromResponse(string response)
        {
            // Extract JSON from markdown code blocks
            var jsonStart = response.IndexOf("```json");
            if (jsonStart >= 0)
            {
                jsonStart = response.IndexOf('\n', jsonStart) + 1;
                var jsonEnd = response.IndexOf("```", jsonStart);
                return response.Substring(jsonStart, jsonEnd - jsonStart).Trim();
            }

            // Try to find JSON object directly
            jsonStart = response.IndexOf('{');
            if (jsonStart >= 0)
            {
                var jsonEnd = response.LastIndexOf('}');
                return response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }

            throw new Exception("Could not extract JSON from agent response");
        }

        private string BuildOrderingContext(
            WorkOrder workOrder,
            List<InventoryItem> inventory,
            List<Supplier> suppliers)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("# Parts Ordering Analysis Request");
            sb.AppendLine();
            sb.AppendLine("## Work Order Information");
            sb.AppendLine($"- Work Order ID: {workOrder.Id}");
            sb.AppendLine($"- Machine ID: {workOrder.MachineId}");
            sb.AppendLine($"- Fault Type: {workOrder.FaultType}");
            sb.AppendLine($"- Priority: {workOrder.Priority}");
            sb.AppendLine();

            sb.AppendLine("## Required Parts");
            foreach (var part in workOrder.RequiredParts)
            {
                sb.AppendLine($"- **{part.PartName}** (Part#: {part.PartNumber})");
                sb.AppendLine($"  * Quantity needed: {part.Quantity}");
                sb.AppendLine($"  * Available in stock: {(part.IsAvailable ? "YES" : "NO")}");
            }
            sb.AppendLine();

            sb.AppendLine("## Current Inventory Status");
            if (inventory.Any())
            {
                foreach (var item in inventory)
                {
                    var needsOrder = item.CurrentStock <= item.ReorderPoint;
                    sb.AppendLine($"- **{item.PartName}** (Part#: {item.PartNumber})");
                    sb.AppendLine($"  * Current Stock: {item.CurrentStock}");
                    sb.AppendLine($"  * Minimum Stock: {item.MinStock}");
                    sb.AppendLine($"  * Reorder Point: {item.ReorderPoint}");
                    sb.AppendLine($"  * Status: {(needsOrder ? "⚠️  NEEDS ORDERING" : "✓ Adequate")}");
                    sb.AppendLine($"  * Location: {item.Location}");
                }
            }
            else
            {
                sb.AppendLine("⚠️  No inventory records found for required parts.");
            }
            sb.AppendLine();

            sb.AppendLine("## Available Suppliers");
            if (suppliers.Any())
            {
                foreach (var supplier in suppliers)
                {
                    sb.AppendLine($"- **{supplier.Name}** (ID: {supplier.Id})");
                    sb.AppendLine($"  * Lead Time: {supplier.LeadTimeDays} days");
                    sb.AppendLine($"  * Reliability: {supplier.Reliability}");
                    sb.AppendLine($"  * Contact: {supplier.ContactEmail}");
                    sb.AppendLine($"  * Parts Available: {string.Join(", ", supplier.Parts.Take(5))}{(supplier.Parts.Count > 5 ? "..." : "")}");
                }
            }
            else
            {
                sb.AppendLine("⚠️  No suppliers found for required parts!");
            }
            sb.AppendLine();

            sb.AppendLine("## Analysis Required");
            sb.AppendLine("Based on the above information, please:");
            sb.AppendLine("1. Determine which parts need to be ordered");
            sb.AppendLine("2. Select the optimal supplier considering:");
            sb.AppendLine("   - Reliability rating (prefer High > Medium > Low)");
            sb.AppendLine("   - Lead time (prefer shorter)");
            sb.AppendLine("   - Part availability");
            sb.AppendLine("3. Calculate:");
            sb.AppendLine("   - Expected delivery date");
            sb.AppendLine("   - Total order cost");
            sb.AppendLine("4. Assign order urgency (CRITICAL if priority=CRITICAL, HIGH if priority=HIGH, otherwise NORMAL)");
            sb.AppendLine();
            sb.AppendLine("Please respond in JSON format:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"supplierId\": \"<selected supplier ID>\",");
            sb.AppendLine("  \"supplierName\": \"<supplier name>\",");
            sb.AppendLine("  \"orderItems\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"partNumber\": \"<part number>\",");
            sb.AppendLine("      \"partName\": \"<part name>\",");
            sb.AppendLine("      \"quantity\": <number>,");
            sb.AppendLine("      \"unitCost\": <decimal>,");
            sb.AppendLine("      \"totalCost\": <decimal>");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"totalCost\": <decimal>,");
            sb.AppendLine("  \"expectedDeliveryDate\": \"<ISO datetime>\",");
            sb.AppendLine("  \"reasoning\": \"<explanation of supplier selection and order decisions>\"");
            sb.AppendLine("}");
            sb.AppendLine("```");

            return sb.ToString();
        }
    }
}
