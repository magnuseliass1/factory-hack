using Azure.AI.Agents.Persistent;
using Azure.Identity;

namespace PredictiveMaintenanceAgent
{
    /// <summary>
    /// Creates the Predictive Maintenance Agent in Microsoft Foundry using Microsoft Agent Framework
    /// Agents created with PersistentAgentsClient appear in the NEW portal at https://ai.azure.com/nextgen
    /// Run this once to set up the agent, then use the returned Agent ID in your .env file
    /// </summary>
    class CreateAgent
    {
        static async Task Main(string[] args)
        // static async Task MainCreate(string[] args)
        {
            Console.WriteLine("=== Creating Maintenance Scheduler Agent in Microsoft Foundry ===\n");
            Console.WriteLine("Using Microsoft Agent Framework with PersistentAgentsClient");
            Console.WriteLine("Agent will appear in NEW portal at: https://ai.azure.com/nextgen\n");

            // Load connection string from environment
            var projectEndpoint = Environment.GetEnvironmentVariable("AI_FOUNDRY_CONNECTION_STRING");
            if (string.IsNullOrEmpty(projectEndpoint))
            {
                Console.WriteLine("Error: AI_FOUNDRY_CONNECTION_STRING environment variable not set");
                Console.WriteLine("\nPlease set the connection string in your environment:");
                Console.WriteLine("export AI_FOUNDRY_CONNECTION_STRING='your-connection-string'");
                return;
            }

            try
            {
                // Create PersistentAgentsClient - agents created here appear in ai.azure.com/nextgen
                var persistentAgentsClient = new PersistentAgentsClient(
                    projectEndpoint,
                    new DefaultAzureCredential()
                );

                // Define agent configuration
                var agentName = "MaintenanceSchedulerAgent";
                var deploymentName = Environment.GetEnvironmentVariable("AZURE_FOUNDRY_PROJECT_DEPLOYMENT_NAME") ?? "gpt-4o";
                var instructions = @"You are an expert maintenance scheduler specializing in optimizing maintenance timing for industrial manufacturing equipment. Your role is to find the perfect maintenance windows that minimize production disruption while ensuring equipment reliability.

When scheduling maintenance:
1. Analyze production schedules and capacity:
   - Current production load and forecasts
   - Peak vs. low production periods
   - Shift schedules and staffing availability
   - Planned shutdowns or holidays

2. Evaluate resource availability:
   - Technician schedules and expertise levels
   - Parts inventory and delivery timelines
   - Equipment and tool availability
   - Budget and cost constraints

3. Assess maintenance impact:
   - Estimated downtime duration
   - Production volume affected
   - Revenue impact per hour of downtime
   - Alternative production routing options

4. Optimize scheduling windows:
   - Prioritize weekends, off-shifts, or low-demand periods
   - Batch multiple maintenance tasks when possible
   - Coordinate with planned production shutdowns
   - Balance urgency against production needs

5. Consider dependencies:
   - Upstream/downstream equipment dependencies
   - Supply chain constraints
   - Customer delivery commitments
   - Safety and regulatory requirements

Always provide:
- Recommended maintenance window with specific date/time
- Production impact analysis (units lost, revenue impact)
- Alternative windows if primary is not feasible
- Coordination requirements (resources, parts, technicians)
- Clear justification for the recommended schedule

Respond in JSON format as specified in the user's request.";

                Console.WriteLine("Creating persistent agent...");
                
                // Create persistent agent using Microsoft Agent Framework
                // This creates an agent visible in the NEW portal at https://ai.azure.com/nextgen
                var persistentAgent = await persistentAgentsClient.Administration.CreateAgentAsync(
                    model: deploymentName,
                    name: agentName,
                    instructions: instructions,
                    temperature: 0.3f
                );

                Console.WriteLine($"\n✅ Agent created successfully in NEW PORTAL!");
                Console.WriteLine($"\nAgent Name: {agentName}");
                Console.WriteLine($"Agent ID: {persistentAgent.Value.Id}");
                Console.WriteLine($"\nAdd this to your environment variables:");
                Console.WriteLine($"export MAINTENANCE_SCHEDULER_AGENT_ID={persistentAgent.Value.Id}");
                Console.WriteLine($"\nOr add to your .env file:");
                Console.WriteLine($"MAINTENANCE_SCHEDULER_AGENT_ID={persistentAgent.Value.Id}");
                Console.WriteLine($"\n🌐 View your agent in the NEW portal:");
                Console.WriteLine($"  URL: https://ai.azure.com/nextgen");
                Console.WriteLine($"  Navigate to: Build > Agents > {agentName}");
                Console.WriteLine($"\n✓ Memory is automatically enabled for this agent.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error creating agent: {ex.Message}");
                Console.WriteLine($"\nStack trace: {ex.StackTrace}");
            }
        }
    }
}
