# Quick Start: Intelligent Repair Planner Agent

**Feature**: 001-repair-planner-agent  
**Date**: 2026-01-13  
**Prerequisites**: .NET 10 SDK, Azure subscription, Cosmos DB account

## 1. Development Environment Setup

### Install Prerequisites
```bash
# Install .NET 10 SDK
winget install Microsoft.DotNet.SDK.10

# Verify installation
dotnet --version  # Should show 10.x.x

# Install Azure CLI (for local development)
winget install Microsoft.AzureCLI
```

### Create Project Structure
```bash
# Create solution and projects
dotnet new sln -n RepairPlannerAgent
dotnet new console -n RepairPlannerAgent -f net10.0
dotnet new xunit -n RepairPlannerAgent.Tests -f net10.0

# Add projects to solution
dotnet sln add RepairPlannerAgent/RepairPlannerAgent.csproj
dotnet sln add RepairPlannerAgent.Tests/RepairPlannerAgent.Tests.csproj

# Add project reference for testing
cd RepairPlannerAgent.Tests
dotnet add reference ../RepairPlannerAgent/RepairPlannerAgent.csproj
```

### Install NuGet Packages
```bash
# Main project dependencies
cd RepairPlannerAgent
dotnet add package Microsoft.Azure.Cosmos --version 3.38.1
dotnet add package Azure.AI.OpenAI --version 1.0.0-beta.14
dotnet add package Microsoft.Extensions.Hosting --version 8.0.0
dotnet add package Microsoft.Extensions.Configuration --version 8.0.0
dotnet add package Microsoft.Extensions.Configuration.EnvironmentVariables --version 8.0.0
dotnet add package Microsoft.Extensions.Logging --version 8.0.0
dotnet add package Microsoft.Extensions.Logging.Console --version 8.0.0
dotnet add package Microsoft.Extensions.DependencyInjection --version 8.0.0

# Testing project dependencies
cd ../RepairPlannerAgent.Tests
dotnet add package NSubstitute --version 5.1.0
dotnet add package Microsoft.NET.Test.Sdk --version 17.8.0
dotnet add package FluentAssertions --version 6.12.0
dotnet add package Microsoft.AspNetCore.Mvc.Testing --version 8.0.0
```

## 2. Configuration Setup

### Environment Variables
Create a `.env` file in the project root:

```bash
# Azure Cosmos DB Configuration
COSMOS_DB_ENDPOINT=https://your-cosmosdb-account.documents.azure.com:443/
COSMOS_DB_KEY=your-primary-key-here
COSMOS_DB_DATABASE_NAME=RepairPlannerDb

# Azure AI Foundry Configuration  
AZURE_OPENAI_ENDPOINT=https://your-foundry-endpoint.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key-here
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4

# Application Configuration
LOG_LEVEL=Information
ENVIRONMENT=Development
```

### appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "CosmosDb": {
    "Endpoint": "",
    "DatabaseName": "RepairPlannerDb",
    "Containers": {
      "Faults": "faults",
      "Technicians": "technicians", 
      "Parts": "parts",
      "WorkOrders": "workorders"
    }
  },
  "AIFoundry": {
    "Endpoint": "",
    "DeploymentName": "gpt-4",
    "MaxTokens": 4000,
    "Temperature": 0.3
  },
  "RepairPlanner": {
    "MaxPlanGenerationTimeSeconds": 30,
    "DefaultTechnicianAssignmentStrategy": "SkillMatch",
    "RequirePartsAvailability": true
  }
}
```

## 3. Core Implementation

### 1. Create Models (Models/DiagnosedFault.cs)
```csharp
namespace RepairPlannerAgent.Models;

/// <summary>
/// Represents a fault detected by upstream diagnostic systems
/// </summary>
public class DiagnosedFault
{
    /// <summary>
    /// Unique fault identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Manufacturing equipment identifier
    /// </summary>
    public string EquipmentId { get; set; } = string.Empty;
    
    /// <summary>
    /// Categorized fault type
    /// </summary>
    public FaultType FaultType { get; set; }
    
    /// <summary>
    /// Fault severity level
    /// </summary>
    public Severity Severity { get; set; }
    
    /// <summary>
    /// UTC timestamp when fault was detected
    /// </summary>
    public DateTime DetectedAt { get; set; }
    
    /// <summary>
    /// Observable symptoms and error codes
    /// </summary>
    public List<string> Symptoms { get; set; } = new();
    
    /// <summary>
    /// Physical location details
    /// </summary>
    public Location Location { get; set; } = new();
    
    /// <summary>
    /// Raw sensor data and diagnostic information
    /// </summary>
    public Dictionary<string, object> DiagnosticData { get; set; } = new();
    
    /// <summary>
    /// Production impact assessment
    /// </summary>
    public string EstimatedImpact { get; set; } = string.Empty;
    
    /// <summary>
    /// Initial skill requirements derived from fault type
    /// </summary>
    public List<string> RequiredSkills { get; set; } = new();
}

public enum FaultType
{
    Mechanical,
    Electrical,
    Hydraulic,
    Pneumatic,
    Software
}

public enum Severity
{
    Critical,
    High,
    Medium,
    Low
}
```

### 2. Create Services (Services/ICosmosDbService.cs)
```csharp
namespace RepairPlannerAgent.Services;

/// <summary>
/// Service interface for Cosmos DB operations
/// </summary>
public interface ICosmosDbService
{
    /// <summary>
    /// Query technicians by required skills and availability
    /// </summary>
    /// <param name="requiredSkills">Skills needed for the repair</param>
    /// <param name="timeWindow">Optional time window for availability</param>
    /// <returns>List of matching technicians</returns>
    Task<IEnumerable<Technician>> QueryTechniciansBySkillsAsync(
        IEnumerable<string> requiredSkills, 
        TimeWindow? timeWindow = null);
    
    /// <summary>
    /// Fetch parts inventory by part numbers
    /// </summary>
    /// <param name="partNumbers">Part numbers to check</param>
    /// <returns>Parts with current inventory status</returns>
    Task<IEnumerable<Part>> FetchPartsInventoryAsync(IEnumerable<string> partNumbers);
    
    /// <summary>
    /// Create work order in Cosmos DB
    /// </summary>
    /// <param name="workOrder">Work order to create</param>
    /// <returns>Created work order with assigned ID</returns>
    Task<WorkOrder> CreateWorkOrderAsync(WorkOrder workOrder);
    
    /// <summary>
    /// Update work order status and details
    /// </summary>
    /// <param name="workOrder">Work order to update</param>
    /// <returns>Updated work order</returns>
    Task<WorkOrder> UpdateWorkOrderAsync(WorkOrder workOrder);
}
```

### 3. Run the Application

```bash
# Build the solution
dotnet build

# Run tests to verify setup
dotnet test

# Run the main application
cd RepairPlannerAgent
dotnet run

# Expected output:
# Starting Repair Planner Agent...
# Initializing Cosmos DB connection...
# Initializing AI Foundry client...
# Processing sample diagnosed fault...
# Generated work order: WO-20260113-001
# Assigned technician: John Smith (Employee ID: EMP001)
# Required parts: 2 items reserved
# Estimated completion: 2.5 hours
# Work order saved to Cosmos DB successfully.
```

## 4. Testing the Implementation

### Unit Test Example
```csharp
[Test]
public async Task GenerateRepairPlan_ValidFault_ReturnsWorkOrder()
{
    // Arrange
    var fault = new DiagnosedFault 
    { 
        Id = "test-fault-001",
        FaultType = FaultType.Hydraulic,
        Severity = Severity.High 
    };
    
    var mockCosmosService = Substitute.For<ICosmosDbService>();
    var mockAIService = Substitute.For<IAIFoundryService>();
    
    // Act
    var planner = new RepairPlanner(mockCosmosService, mockAIService, logger);
    var result = await planner.GenerateRepairPlanAsync(fault);
    
    // Assert
    result.Should().NotBeNull();
    result.FaultId.Should().Be(fault.Id);
    result.Tasks.Should().NotBeEmpty();
}
```

### Integration Test
```bash
# Run integration tests with real Cosmos DB
dotnet test --filter Category=Integration

# Run performance tests
dotnet test --filter Category=Performance
```

## 5. Next Steps

1. **Run `/speckit.tasks`** to generate detailed implementation tasks
2. **Set up Azure resources** (Cosmos DB, AI Foundry deployment)
3. **Implement TDD workflow** following constitutional requirements
4. **Add monitoring and logging** with Application Insights
5. **Configure CI/CD pipeline** for deployment automation

## 6. Troubleshooting

### Common Issues
- **Cosmos DB connection fails**: Verify endpoint and key in environment variables
- **AI Foundry timeout**: Check deployment status and increase timeout settings
- **Part availability errors**: Ensure Cosmos DB containers are properly initialized
- **Test failures**: Run `dotnet restore` and verify all NuGet packages are installed

### Performance Optimization
- Enable Cosmos DB connection pooling
- Implement caching for frequently accessed technician and parts data  
- Use async patterns consistently throughout the application
- Monitor response times and adjust AI model parameters as needed

This quickstart provides a complete development environment setup and basic implementation structure following the constitutional requirements for code quality, testing, and performance.