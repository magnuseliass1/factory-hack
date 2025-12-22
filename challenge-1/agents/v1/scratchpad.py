import asyncio
import os
from agent_framework import ChatAgent
from agent_framework.azure import AzureAIAgentClient
from azure.ai.projects.aio import AIProjectClient
from azure.identity.aio import AzureCliCredential
from agent_framework import HostedWebSearchTool, HostedMCPTool

async def main():
    async with (
        AzureCliCredential() as credential,
        AIProjectClient(
            endpoint=os.environ["AZURE_AI_PROJECT_ENDPOINT"],
            credential=credential
        ) as project_client,
    ):
        # Create a persistent agent
        created_agent = await project_client.agents.create_agent(
            model=os.environ["AZURE_AI_MODEL_DEPLOYMENT_NAME"],
            name="PersistentAgent",
            instructions="You are a helpful assistant."
        )

        try:
            # Use the agent
            async with ChatAgent(
                chat_client=AzureAIAgentClient(
                    project_client=project_client,
                    agent_id=created_agent.id
                ),
                instructions="You are a document assistant",
                tools=[
                        HostedMCPTool(
                            name="Microsoft Learn MCP",
                            url="https://learn.microsoft.com/api/mcp"
                        )
                    ]
            ) as agent:
                result = await agent.run("How do I create an Azure storage account?")
                print(result.text)
        finally:
            # Clean up the agent
            print('finish')
            #await project_client.agents.delete_agent(created_agent.id)

asyncio.run(main())