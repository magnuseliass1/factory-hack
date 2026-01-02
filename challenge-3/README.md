# Challenge 3: Maintenance Scheduler & Parts Ordering Agents

**Expected Duration:** 60 minutes

## Introduction

In this challenge, you'll work with two specialized AI agents that optimize factory operations through intelligent maintenance scheduling and automated supply chain management. Both agents interact with Cosmos DB to read work orders, analyze data, and save their outputs back to the database.

### Agent Overview

**Maintenance Scheduler Agent** (`maintenance_scheduler.py`)
- Analyzes work orders and historical maintenance data
- Finds optimal maintenance windows that minimize production disruption
- Generates predictive maintenance schedules with risk assessment
- Saves schedules to Cosmos DB `MaintenanceSchedules` container
- Updates work order status to 'Scheduled'

**Parts Ordering Agent** (`parts_ordering.py`)
- Checks inventory levels for required parts
- Evaluates supplier performance and lead times
- Generates optimized parts orders with cost analysis
- Saves orders to Cosmos DB `PartsOrders` container
- Updates work order status to 'PartsOrdered' or 'Ready'

### Cosmos DB Integration

Both agents interact with Azure Cosmos DB as their primary data store:

#### Containers Used

| Container | Purpose | Agent Usage |
|-----------|---------|-------------|
| **WorkOrders** | Work orders from Repair Planner | Read by both agents to get job details |
| **Machines** | Equipment information | Referenced for machine context |
| **MaintenanceHistory** | Historical maintenance records | Read by Maintenance Scheduler for pattern analysis |
| **MaintenanceWindows** | Available production windows | Read by Maintenance Scheduler to find optimal timing |
| **MaintenanceSchedules** | Generated maintenance schedules | **Written by Maintenance Scheduler** |
| **PartsInventory** | Current stock levels | Read by Parts Ordering to check availability |
| **Suppliers** | Supplier information | Read by Parts Ordering for sourcing decisions |
| **PartsOrders** | Generated parts orders | **Written by Parts Ordering Agent** |

#### Data Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        MAINTENANCE SCHEDULER AGENT                          │
└─────────────────────────────────────────────────────────────────────────────┘

Input (READ):                         Output (WRITE):
├─ WorkOrders                         ├─ MaintenanceSchedules
├─ MaintenanceHistory                 │  └─ scheduled_date
├─ MaintenanceWindows                 │  └─ risk_score (0-100)
└─ Machines                            │  └─ predicted_failure_probability
                                      │  └─ recommended_action
                                      │  └─ maintenance_window
                                      │  └─ reasoning
                                      └─ WorkOrders (status update to 'Scheduled')

┌─────────────────────────────────────────────────────────────────────────────┐
│                          PARTS ORDERING AGENT                               │
└─────────────────────────────────────────────────────────────────────────────┘

Input (READ):                         Output (WRITE):
├─ WorkOrders                         ├─ PartsOrders
├─ PartsInventory                     │  └─ supplier_id, supplier_name
├─ Suppliers                          │  └─ order_items (part, qty, cost)
└─ Machines                            │  └─ total_cost
                                      │  └─ expected_delivery_date
                                      │  └─ reasoning
                                      └─ WorkOrders (status update to 'PartsOrdered' or 'Ready')
```

---

## Part 1: Maintenance Scheduler Agent

The Maintenance Scheduler Agent analyzes work orders and determines the optimal time to perform maintenance by balancing equipment reliability needs against production impact.

### What It Does

1. **Reads Work Order** from `WorkOrders` container
2. **Analyzes Historical Data** from `MaintenanceHistory` container to understand failure patterns
3. **Checks Available Windows** from `MaintenanceWindows` container to find low-impact periods
4. **Runs AI Analysis** using Microsoft Agent Framework to assess risk and recommend timing
5. **Saves Schedule** to `MaintenanceSchedules` container with risk scores and recommendations
6. **Updates Work Order** status to 'Scheduled'

### Running the Agent

```bash
cd /workspaces/factory-ops-hack/challenge-3
python maintenance_scheduler.py WO-001
```

### Expected Output

```
=== Predictive Maintenance Agent ===

1. Retrieving work order...
   ✓ Work Order: WO-001
   Machine: machine-001
   Fault: Temperature Sensor Malfunction
   Priority: high

2. Analyzing historical maintenance data...
   ✓ Found 3 historical maintenance records

3. Checking available maintenance windows...
   ✓ Found 17 available windows in next 14 days

4. Running AI predictive analysis...
   ✓ Analysis complete!

=== Predictive Maintenance Schedule ===
Schedule ID: sched-1735845678
Machine: machine-001
Scheduled Date: 2026-01-04 22:00
Window: 22:00 - 06:00
Production Impact: Low
Risk Score: 85/100
Failure Probability: 70.0%
Recommended Action: URGENT

Reasoning:
Given the high priority work order and historical pattern of temperature sensor 
failures on machine-001, immediate scheduling is recommended. The selected 
weekend night window (Saturday 10PM - Sunday 6AM) minimizes production impact 
while addressing the critical temperature sensor issue before potential failure.

5. Saving maintenance schedule...
   ✓ Schedule saved to Cosmos DB

6. Updating work order status...
   ✓ Work order status updated to 'Scheduled'

✓ Predictive Maintenance Agent completed successfully!
```

### What Gets Saved to Cosmos DB

**MaintenanceSchedules Container:**
```json
{
  "id": "sched-1735845678",
  "workOrderId": "WO-001",
  "machineId": "machine-001",
  "scheduledDate": "2026-01-04T22:00:00Z",
  "maintenanceWindow": {
    "id": "mw-2026-01-04-night",
    "startTime": "2026-01-04T22:00:00Z",
    "endTime": "2026-01-05T06:00:00Z",
    "productionImpact": "Low"
  },
  "riskScore": 85.0,
  "predictedFailureProbability": 0.70,
  "recommendedAction": "URGENT",
  "reasoning": "Given the high priority work order and historical pattern...",
  "createdAt": "2026-01-02T15:30:00Z"
}
```

**WorkOrders Container (Updated):**
```json
{
  "id": "WO-001",
  "machineId": "machine-001",
  "status": "Scheduled",  // ← Updated from "Created"
  ...
}
```

---

## Part 2: Parts Ordering Agent

The Parts Ordering Agent checks inventory availability and generates optimized parts orders by evaluating supplier reliability, lead times, and costs.

### What It Does

1. **Reads Work Order** from `WorkOrders` container to get required parts
2. **Checks Inventory** from `PartsInventory` container to determine what's in stock
3. **Identifies Missing Parts** that need to be ordered
4. **Finds Suppliers** from `Suppliers` container that can provide the parts
5. **Runs AI Analysis** to optimize supplier selection based on reliability, lead time, and cost
6. **Saves Parts Order** to `PartsOrders` container with order details
7. **Updates Work Order** status to 'PartsOrdered' (or 'Ready' if all parts available)

### Running the Agent

```bash
cd /workspaces/factory-ops-hack/challenge-3
python parts_ordering.py WO-002
```

### Expected Output (When Parts Need Ordering)

```
=== Parts Ordering Agent ===

1. Retrieving work order...
   ✓ Work Order: WO-002
   Machine: machine-002
   Required Parts: 2
   Priority: medium

2. Checking inventory status...
   ✓ Found 2 inventory records

⚠️  2 part(s) need to be ordered:
   - Drum Bearing (Qty: 1)
   - Tension Sensor Module (Qty: 1)

3. Finding suppliers...
   ✓ Found 3 potential suppliers

4. Running AI parts ordering analysis...
   ✓ Parts order generated!

=== Parts Order ===
Order ID: po-1735845789
Work Order: WO-002
Supplier: Industrial Parts Co (ID: SUP-001)
Expected Delivery: 2026-01-07
Total Cost: $1340.00
Status: Pending

Order Items:
  - Drum Bearing (#PART-006)
    Qty: 1 @ $890.00 = $890.00
  - Tension Sensor Module (#PART-005)
    Qty: 1 @ $450.00 = $450.00

5. Saving parts order...
   ✓ Order saved to SCM system

6. Updating work order status...
   ✓ Work order status updated to 'PartsOrdered'

✓ Parts Ordering Agent completed successfully!
```

### Expected Output (When All Parts Available)

```
=== Parts Ordering Agent ===

1. Retrieving work order...
   ✓ Work Order: WO-001
   Machine: machine-001
   Required Parts: 1
   Priority: high

2. Checking inventory status...
   ✓ Found 1 inventory records

✓ All required parts are available in stock!
No parts order needed.

3. Updating work order status...
   ✓ Work order status updated to 'Ready'

✓ Parts Ordering Agent completed successfully!
```

### What Gets Saved to Cosmos DB

**PartsOrders Container:**
```json
{
  "id": "po-1735845789",
  "workOrderId": "WO-002",
  "supplierId": "SUP-001",
  "supplierName": "Industrial Parts Co",
  "orderItems": [
    {
      "partNumber": "PART-006",
      "partName": "Drum Bearing",
      "quantity": 1,
      "unitCost": 890.00,
      "totalCost": 890.00
    },
    {
      "partNumber": "PART-005",
      "partName": "Tension Sensor Module",
      "quantity": 1,
      "unitCost": 450.00,
      "totalCost": 450.00
    }
  ],
  "totalCost": 1340.00,
  "expectedDeliveryDate": "2026-01-07T00:00:00Z",
  "orderStatus": "Pending",
  "reasoning": "Selected Industrial Parts Co based on high reliability rating...",
  "createdAt": "2026-01-02T15:35:00Z"
}
```

**WorkOrders Container (Updated):**
```json
{
  "id": "WO-002",
  "machineId": "machine-002",
  "status": "PartsOrdered",  // ← Updated from "Created"
  ...
}
```

---

## Agent Architecture

Both agents are **self-contained Python files** that include:
- **Data models** (dataclasses for type safety)
- **Cosmos DB service layer** (read/write operations)
- **AI agent logic** using Microsoft Agent Framework
- **Portal registration** using Azure AI Projects SDK
- **Main execution workflow** with error handling

### Key Features

✅ **Cosmos DB Integration**: Direct read/write operations with multiple containers  
✅ **Portal Registration**: Agents automatically register in Azure AI Foundry with versioning  
✅ **Self-Contained**: Each agent is fully independent with no cross-dependencies  
✅ **Azure Authentication**: Uses `DefaultAzureCredential` for seamless auth  
✅ **Production-Ready**: Async/await, error handling, proper resource cleanup  
✅ **AI-Powered Decision Making**: Uses GPT-4 for intelligent analysis and recommendations  

## Viewing Agents in Azure AI Foundry Portal

After running the agents, you can view them in the Azure AI Foundry portal:

1. **Navigate to**: https://ai.azure.com
2. **Select your project**: Look for your project (e.g., `msagthack-aiproject-...`)
3. **Go to**: Build → Agents (or Assistants)
4. **You should see**:
   - `MaintenanceSchedulerAgent` - Predictive maintenance scheduling
   - `PartsOrderingAgent` - Parts ordering automation

Each agent includes:
- Model configuration (gpt-4o-mini)
- System instructions
- Version metadata
- Creation timestamp

## Learn More

Congratulations! You've successfully worked with two AI agents that integrate deeply with Cosmos DB. You've learned how to:

✅ **Read from Cosmos DB** - Query work orders, history, inventory, and supplier data  
✅ **Write to Cosmos DB** - Save generated schedules and parts orders  
✅ **Update existing records** - Change work order status based on agent actions  
✅ **Use Microsoft Agent Framework** - Modern agent architecture with `ChatAgent`  
✅ **Integrate with Azure AI Foundry** - Register agents and use deployed models  
✅ **Build data-driven agents** - Combine database queries with AI analysis  
✅ **Handle multiple containers** - Work with complex data relationships  

These agents demonstrate how AI can optimize factory operations by:
- **Predictive Scheduling**: Finding optimal maintenance windows using historical data
- **Risk Assessment**: Calculating failure probability and recommending actions
- **Inventory Management**: Automatically checking stock and ordering needed parts
- **Supplier Optimization**: Selecting best suppliers based on reliability and lead time
- **Workflow Automation**: Updating work order status as tasks complete