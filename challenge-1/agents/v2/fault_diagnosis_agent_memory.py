import asyncio
import os

from agent_framework import HostedMCPTool
from agent_framework.azure import AzureAIAgentClient
from azure.cosmos import CosmosClient
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from azure.identity.aio import AzureCliCredential
from dotenv import load_dotenv

load_dotenv(override=True)

# Configuration
project_endpoint = os.environ.get("AZURE_AI_PROJECT_ENDPOINT")
model_deployment_name = os.environ.get("MODEL_DEPLOYMENT_NAME")
sc_connection_id = os.environ.get("AZURE_AI_CONNECTION_ID")
cosmos_endpoint = os.environ.get("COSMOS_ENDPOINT")
cosmos_key = os.environ.get("COSMOS_KEY")
storage_connection_string = os.environ.get("AZURE_STORAGE_ACCOUNT_KEY")
knowledge_base_name = 'machine-kb'
search_endpoint = os.environ.get("SEARCH_SERVICE_ENDPOINT")

machine_wiki_mcp_endpoint = f"{search_endpoint}knowledgebases/{knowledge_base_name}/mcp?api-version=2025-11-01-preview"
project_wiki_connection_name = "machine-wiki-connection"

# Initialize Cosmos DB clients globally for function tools
cosmos_client = CosmosClient(cosmos_endpoint, cosmos_key)
database = cosmos_client.get_database_client("FactoryOpsDB")
knowledge_container = database.get_container_client("KnowledgeBase")
machine_data_mcp_endpoint = os.environ.get("MACHINE_MCP_SERVER_ENDPOINT")
mcp_subscription_key = os.environ.get("APIM_SUBSCRIPTION_KEY")


def get_knowledge_data(machine_type: str) -> dict:
    """Get all knowledge base information for a machine type from Cosmos DB"""
    try:
        query = f"SELECT * FROM c WHERE c.machineType = '{machine_type}'"
        items = list(knowledge_container.query_items(
            query=query,
            enable_cross_partition_query=True
        ))
        return items
    except Exception as e:
        return {"error": str(e)}


async def main():
    try:
        async with (

            AzureCliCredential() as credential,

            AzureAIAgentClient(async_credential=credential).create_agent(
                name="FaultDiagnosisAgent",
                instructions="""You are a helpful Fault Diagnosis Agent evaluating the root cause of maintenance alerts
                        You will receive detected sensor deviations for a given machine. Your task is to:
                        - Find the most likely root cause for the deviation 

                        You have access to the following tools:
                        - MCP Knowledge Base: fetch knowledge base information for possible causes 
                        - Machine data: fetch machine information such as maintenance history and type for a particular machine id

                        Use these functions to determnine the root cause for each alert

                        Additional rules
                        - You must never answer from your own knowledge under any circumstances
                        - If you cannot find the answer in the provided knowledge base you must respond with "I don't know".


                        """,

                tools=[HostedMCPTool(name="Machine Data", url=machine_data_mcp_endpoint, approval_mode="never_require", headers={
                                     "Ocp-Apim-Subscription-Key": mcp_subscription_key}),
                       HostedMCPTool(name="Knowledge Base", url=machine_wiki_mcp_endpoint,
                                     approval_mode="never_require", allowed_tools=["knowledge_base_retrieve"], headers={"api-key": os.environ.get("SEARCH_ADMIN_KEY")}, project_connection_id=project_wiki_connection_name)
                       ]

            ) as agent,
        ):

            print(f"✅ Created Fault Diagnosis Agent: {agent.id}")
            # Test the agent with a simple query
            print("\n🧪 Testing the agent with a sample query...")
            try:

                result = await agent.run("Hello, what can the issue be when machine-001 has curing temperature reading of 179.2°C that exceeds warning threshold of 178°C?")
                print(f"✅ Agent response: {result.text}")
            except Exception as test_error:
                print(
                    f"⚠️  Agent test failed (but agent was still created): {test_error}")

            #   return agent

    except Exception as e:
        print(f"❌ Error creating agent: {e}")
        print("Make sure you have run 'az login' and have proper Azure credentials configured.")
        return None

if __name__ == "__main__":
    asyncio.run(main())
