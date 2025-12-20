# Challenge 3: Maintenance Scheduler & Parts Ordering Agents

**Expected Duration:** 60 minutes

## Introduction

In this challenge, you'll build two specialized AI agents that work together to optimize factory operations through intelligent maintenance scheduling and automated supply chain management:

- **Maintenance Scheduler Agent**: Finds optimal maintenance windows that minimize production disruption while ensuring equipment reliability. It analyzes production schedules, resource availability, and operational constraints to recommend the perfect timing for scheduled maintenance activities.

- **Parts Ordering Agent**: Monitors inventory levels, evaluates supplier performance, and automates parts ordering to ensure required components are available when needed. It optimizes order timing, supplier selection, and delivery schedules to support maintenance operations.


### What is Agent Memory?

In this challenge, we implement **chat history memory** using the Microsoft Agent Framework pattern. This allows agents to maintain context across multiple interactions by storing conversation history in Cosmos DB.

**Chat History Memory** stores the conversation messages (both user and assistant) for each entity:
- The **Maintenance Scheduler Agent** maintains separate chat histories for each machine, allowing it to learn scheduling preferences, production patterns, and optimal maintenance windows over time
- The **Parts Ordering Agent** maintains separate chat histories for each work order, helping it learn from past supplier performance and ordering decisions

This implementation follows the **AgentWithMemory_Step01_ChatHistoryMemory** pattern from the Microsoft Agent Framework, providing persistent context without requiring complex vector embeddings or portal-managed threads.

### How Memory Works in Our Agents

Our agents use **chat history memory** stored in Cosmos DB, following the Microsoft Agent Framework's memory pattern:

#### Chat History Memory (Conversation Context)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    MAINTENANCE SCHEDULER AGENT MEMORY                       │
│                    (Cosmos DB ChatHistories Container)                      │
└─────────────────────────────────────────────────────────────────────────────┘

  Session 1              Session 2              Session 3
  ─────────              ─────────              ─────────
     │                       │                       │
     ▼                       ▼                       ▼
┌─────────────────────────────────────────────────────────────┐
│  Chat History: Machine-001 (Last 10 messages)               │
│  ───────────────────────────────────────────────────────   │
│  User: "Find maintenance window for Machine-001"            │
│  Agent: "Optimal window: Sat 3AM-7AM, 0 production impact"  │
│  User: "What about weekday options?"                        │
│  Agent: "Tuesday 11PM-3AM: 15% production, saves $2K"       │
│  User: "Schedule for Saturday"                              │
│  Agent: "Confirmed: Sat 3AM, technician assigned, parts OK" │
└─────────────────────────────────────────────────────────────┘
                         ▲
                         │
                Cosmos DB (JSON)
              {id, machineId, history}

Example document in ChatHistories container:
```json
{
  "id": "machine-001-history",
  "entityId": "machine-001",
  "entityType": "machine",
  "purpose": "maintenance_scheduling",
  "historyJson": "[{\"toRole\":\"user\",\"content\":\"Find optimal maintenance window for Machine-001\"},{\"toRole\":\"assistant\",\"content\":\"Optimal window: Saturday 3AM-7AM. Zero production impact, technicians available, estimated 4hr downtime.\"},{\"toRole\":\"user\",\"content\":\"What about weekday options?\"},{\"toRole\":\"assistant\",\"content\":\"Alternative: Tuesday 11PM-3AM affects 15% production but saves $2K in weekend premium costs.\"}]",
  "updatedAt": "2025-12-20T10:30:00Z"
}
```

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      PARTS ORDERING AGENT MEMORY                            │
│                    (Cosmos DB ChatHistories Container)                      │
└─────────────────────────────────────────────────────────────────────────────┘

  Session 1              Session 2              Session 3
  ─────────              ─────────              ─────────
     │                       │                       │
     ▼                       ▼                       ▼
┌─────────────────────────────────────────────────────────────┐
│  Chat History: WO-2024-445 (Last 10 messages)               │
│  ───────────────────────────────────────────────────────    │
|  User: "Order parts from Supplier-A"                        │
│  Agent: "Ordered Belt-B2000, ETA: 5 days"                   │
│  User: "Update: Supplier-A delayed to 8 days"               │
│  Agent: "Noted: Supplier-A reliability decreased"           │
│  User: "Order same part again"                              │
│  Agent: "Recommending Supplier-B (better track record)"     │
└─────────────────────────────────────────────────────────────┘
                         ▲
                         │
                Cosmos DB (JSON)
           {id, workOrderId, history}
```

**Chat History Memory Benefits:**
- **Persistent Context**: Chat history survives across sessions and application restarts
- **Entity-Specific Intelligence**: Each machine or work order maintains its own conversation history
- **Simple & Reliable**: Direct message storage in Cosmos DB - no complex embeddings or indexing
- **Token-Efficient**: Stores only last 10 messages per entity to manage context window

### How It Works

1. **First request** for Machine-001 → Creates empty chat history, processes request, saves conversation to Cosmos DB `ChatHistories` container
2. **Second request** for Machine-001 → Retrieves previous messages, adds to context, processes request, saves updated history
3. **Request** for Machine-002 → Creates separate chat history, independent from Machine-001
4. **Service restart** → Chat histories persist in Cosmos DB, no memory lost

## 1. Create Maintenance Scheduler Agent

1. Edit `MaintenanceSchedulerAgent/CreateAgent.cs`:
   - Uncomment line 12: `static async Task Main(string[] args)`
   - Comment out line 13: `// static async Task MainCreate(string[] args)`

2. Edit `MaintenanceSchedulerAgent/Program.cs`:
   - Comment out line 9: `// static async Task Main(string[] args)`

3. Run the creation script:
```bash
cd /workspaces/factory-ops-hack/challenge-3/MaintenanceSchedulerAgent
set -a && source ../../.env && set +a
dotnet run
```

**What gets created:**
- ✓ Maintenance Scheduler Agent (visible in the NEW Azure AI Foundry portal at https://ai.azure.com/nextgen)
- ✓ Agent specialized in finding optimal maintenance windows with minimal production disruption
- ✓ Configuration includes production schedule analysis, resource availability, and downtime optimization

**Note:** The agent is created in the portal using `PersistentAgentsClient`, but the actual execution uses direct Azure OpenAI API calls with chat history stored in Cosmos DB. The portal agent serves as a configuration reference.

## 2. Create Parts Ordering Agent

1. Edit `PartsOrderingAgent/CreateAgent.cs`:
   - Uncomment `static async Task Main(string[] args)`
   - Comment out the alternative Main method name

2. Edit `PartsOrderingAgent/Program.cs`:
   - Comment out `static async Task Main(string[] args)`

3. Run the creation script:
```bash
cd /workspaces/factory-ops-hack/challenge-3/PartsOrderingAgent
set -a && source ../../.env && set +a
dotnet run
```

Copy the Agent IDs from the output and they will be automatically added to your `.env` file.

## 3. Configure Environment Variables

Your `.env` file should contain (automatically added during creation):

```bash
MAINTENANCE_SCHEDULER_AGENT_ID=<maintenance-scheduler-agent-id>
PARTS_ORDERING_AGENT_ID=<parts-ordering-agent-id>
COSMOS_DATABASE_NAME=FactoryOpsDB
```

**Note:** The current implementation expects specific Cosmos DB containers (WorkOrders, Machines, Telemetry, etc.) created by Challenge 0. If you encounter "container not found" errors, verify that Challenge 0 data seeding completed successfully.

## 4. Running the Maintenance Scheduler Agent

**Before running, ensure you've reverted the Main method edits:**
1. Edit `MaintenanceSchedulerAgent/Program.cs`: Uncomment line 9 `static async Task Main(string[] args)`
2. Edit `MaintenanceSchedulerAgent/CreateAgent.cs`: Comment out line 12 `// static async Task Main(string[] args)`

```bash
cd /workspaces/factory-ops-hack/challenge-3/MaintenanceSchedulerAgent
set -a && source ../../.env && set +a
dotnet run wo-2024-445
```

**Expected Output:**
1. ✓ Retrieves work order from Cosmos DB (ERP container)
2. ✓ Analyzes production schedules and capacity (MES container)
3. ✓ Checks technician and resource availability
4. ✓ Evaluates maintenance windows for minimal disruption
5. ✓ Saves optimal maintenance schedule to Cosmos DB
6. ✓ Updates work order status to 'Scheduled'

**What it does:**
- Analyzes production schedules and identifies low-impact periods
- Evaluates resource availability (technicians, parts, equipment)
- Calculates production impact and revenue loss for different windows
- Recommends optimal maintenance timing with justification
- Coordinates dependencies and constraints
- Balances urgency against production needs
- Considers production impact and urgency

## 5. Running the Parts Ordering Agent

**Before running, ensure you've reverted the Main method edits:**
1. Edit `PartsOrderingAgent/Program.cs`: Uncomment `static async Task Main(string[] args)`
2. Edit `PartsOrderingAgent/CreateAgent.cs`: Comment out `static async Task Main(string[] args)`

```bash
cd /workspaces/factory-ops-hack/challenge-3/PartsOrderingAgent
set -a && source ../../.env && set +a
dotnet run wo-2024-456
```

**Expected Output:**
1. ✓ Retrieves work order from Cosmos DB
2. ✓ Checks inventory status for required parts (WMS container)
3. ✓ Identifies parts needing ordering
4. ✓ Finds available suppliers (SCM container)
5. ✓ Generates optimized parts order using AI
6. ✓ Saves order to SCM system
7. ✓ Updates work order status to 'PartsOrdered'

**What it does:**
- Analyzes inventory levels against reorder points
- Selects optimal suppliers based on reliability and lead time
- Calculates expected delivery dates
- Optimizes order costs
- Determines order urgency
Chat history per machine


**How it works:**
1. First request for Machine-001 → Creates empty chat history, processes request, saves messages to Cosmos DB `ChatHistories` container
2. Second request for Machine-001 → Retrieves previous messages, adds to context, processes request, saves updated history
3. Request for Machine-002 → Creates separate chat history, independent from Machine-001
4. Service restart → Chat histories persist in Cosmos DB, no memory lost

**Benefits of this approach:**
- **Simple & Reliable**: Direct JSON storage in Cosmos DB - no complex indexing or vector embeddings
- **Survives restarts**: Chat history persists even when the application restarts
- **Token-Efficient**: Stores only last 10 messages per entity to keep context window manageable
- **Entity-Specific**: Each machine/work order maintains its own conversation context
- **Transparent**: Easy to inspect, debug, and understand stored conversation data

## Learn More

Congratulations! You've built two AI agents that work together to optimize maintenance scheduling and automated parts ordering. You've learned how to:

✅ Create persistent agents programmatically using Microsoft Agent Framework  
✅ Implement chat history memory with Cosmos DB persistence  
✅ Use direct Azure OpenAI API calls for agent execution  
✅ Integrate agents with Cosmos DB for data access and memory storage  
✅ Manage conversation context across multiple sessions  
✅ Build AI-powered decision systems for industrial operations  

These agents demonstrate how AI can optimize factory operations by:
- Finding optimal maintenance windows that minimize production disruption
- Analyzing production schedules, resources, and constraints for intelligent scheduling
- Automating inventory management and supplier selection
- Maintaining conversation context across sessions for contextual decision-making
- Learning from previous interactions stored in chat history