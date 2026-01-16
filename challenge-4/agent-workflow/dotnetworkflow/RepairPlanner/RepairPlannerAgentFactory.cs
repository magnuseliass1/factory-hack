using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using FactoryWorkflow.RepairPlanner.Models;
using FactoryWorkflow.RepairPlanner.Services;

namespace FactoryWorkflow.RepairPlanner;

/// <summary>
/// Factory for creating the RepairPlanner AIAgent with Cosmos DB tools.
/// </summary>
public static class RepairPlannerAgentFactory
{
    private const string DefaultInstructions = """
        You are a Repair Planner Agent for factory maintenance operations.
        
        Your role is to analyze diagnosed faults and create detailed repair work orders.
        
        When you receive a fault diagnosis, you should:
        1. Use the GetAvailableTechnicians tool to find technicians with the required skills
        2. Use the GetAvailableParts tool to check parts inventory
        3. Create a detailed repair plan based on the fault and available resources
        4. Use the CreateWorkOrder tool to save the work order to the database
        
        Output your repair plan in a structured format with:
        - Work Order ID (from CreateWorkOrder result)
        - Machine ID (from the input)
        - Fault Type (from the diagnosis)
        - Priority (critical/high/medium/low based on severity)
        - Assigned Technician (from GetAvailableTechnicians)
        - Estimated Duration (in minutes)
        - Repair Tasks (numbered list of steps)
        - Required Parts (from GetAvailableParts)
        - Safety Notes (any precautions)
        
        Always call the tools to get real data from the database.
        """;

    /// <summary>
    /// Creates a RepairPlanner AIAgent with optional Cosmos DB tools.
    /// </summary>
    public static AIAgent Create(
        string azureOpenAIEndpoint,
        string deployment,
        CosmosDbService? cosmosService = null,
        ILoggerFactory? loggerFactory = null)
    {
        var tools = new List<AITool>();

        if (cosmosService != null)
        {
            tools.AddRange(CreateCosmosTools(cosmosService));
        }

        return new AzureOpenAIClient(new Uri(azureOpenAIEndpoint), new DefaultAzureCredential())
            .GetChatClient(deployment)
            .AsIChatClient()
            .CreateAIAgent(
                instructions: DefaultInstructions,
                name: "RepairPlannerAgent",
                tools: tools.Count > 0 ? tools : null);
    }

    /// <summary>
    /// Creates the Cosmos DB tools for the RepairPlanner agent.
    /// </summary>
    private static IEnumerable<AITool> CreateCosmosTools(CosmosDbService cosmosService)
    {
        // Tool to get available technicians
        yield return AIFunctionFactory.Create(
            async (string[] requiredSkills, string department) =>
            {
                var technicians = await cosmosService.GetAvailableTechniciansWithSkillsAsync(
                    requiredSkills.ToList(),
                    department,
                    requireAllSkills: false);
                return JsonSerializer.Serialize(technicians.Take(5));
            },
            "GetAvailableTechnicians",
            "Gets available technicians with the specified skills from the database. " +
            "Parameters: requiredSkills (array of skill names like 'electrical', 'mechanical', 'hydraulic'), " +
            "department (e.g., 'Maintenance')");

        // Tool to get available parts
        yield return AIFunctionFactory.Create(
            async (string[] partNumbers) =>
            {
                var parts = await cosmosService.GetPartsByPartNumbersAsync(partNumbers.ToList());
                return JsonSerializer.Serialize(parts);
            },
            "GetAvailableParts",
            "Gets parts from inventory by their part numbers. " +
            "Parameters: partNumbers (array of part numbers like 'BELT-001', 'MOTOR-002')");

        // Tool to create a work order
        yield return AIFunctionFactory.Create(
            async (string machineId, string faultType, string priority, string assignedTo,
                   int estimatedMinutes, string description, string[] partNumbers) =>
            {
                var workOrder = new WorkOrder
                {
                    Id = $"wo-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString()[..8]}",
                    MachineId = machineId,
                    FaultType = faultType,
                    Priority = priority,
                    AssignedTo = assignedTo,
                    EstimatedDurationMinutes = estimatedMinutes,
                    Description = description,
                    Status = "scheduled",
                    CreatedDate = DateTimeOffset.UtcNow,
                    RequiredParts = partNumbers.Select(p => new PartRequirement { PartNumber = p, Quantity = 1 }).ToList()
                };
                var id = await cosmosService.CreateWorkOrderAsync(workOrder);
                return JsonSerializer.Serialize(new { workOrderId = id, status = "created" });
            },
            "CreateWorkOrder",
            "Creates a new work order in the database. " +
            "Parameters: machineId, faultType, priority (critical/high/medium/low), assignedTo (technician id), " +
            "estimatedMinutes, description, partNumbers (array of required part numbers)");
    }
}
