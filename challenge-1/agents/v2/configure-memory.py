# List knowledge sources by name and type
import os
import time

from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import (
    MemorySearchTool,
    MemoryStoreDefaultDefinition,
    MemoryStoreDefaultOptions,
    PromptAgentDefinition,
)
from azure.identity import DefaultAzureCredential

project_endpoint = os.environ.get("AZURE_AI_PROJECT_ENDPOINT")
model_deployment_name = os.environ.get("MODEL_DEPLOYMENT_NAME")
memory_store_name = "fault_diagnosis_memory_store"
agent_name = "MemoryTestAGent"


def create_memory_store():
    # Initialize the client
    client = AIProjectClient(
        endpoint=project_endpoint,
        credential=DefaultAzureCredential()
    )

    # Specify memory store options
    options = MemoryStoreDefaultOptions(
        chat_summary_enabled=True,
        user_profile_enabled=True,
        user_profile_details="Avoid irrelevant or sensitive data, such as age, financials, precise location, and credentials"
    )

    # Create memory store
    definition = MemoryStoreDefaultDefinition(
        chat_model=model_deployment_name,  # Your chat model deployment name
        # Your embedding model deployment name
        embedding_model="text-embedding-3-small",
        options=options
    )

    memory_store = client.memory_stores.create(
        name="fault_diagnosis_memory_store",
        definition=definition,
        description=memory_store_name,
    )

    print(f"Created memory store: {memory_store.name}")


def list_memory_stores():

    # Initialize the client
    client = AIProjectClient(
        endpoint=project_endpoint,
        credential=DefaultAzureCredential()
    )

    # List all memory stores
    stores_list = client.memory_stores.list()

    for store in stores_list:
        print(f"- {store.name} ({store.description})")


def test_with_agent():
    # Set scope to associate the memories with
    # You can also use "{{$userId}}" to take the oid of the request authentication header
    scope = "user_123"

    # Initialize the client
    client = AIProjectClient(
        endpoint=project_endpoint,
        credential=DefaultAzureCredential()
    )

    # Create memory search tool
    tool = MemorySearchTool(
        memory_store_name=memory_store_name,
        scope=scope,
        update_delay=1,  # Wait 1 second of inactivity before updating memories
        # In a real application, set this to a higher value like 300 (5 minutes, default)
    )

    # Create a prompt agent with memory search tool
    agent = client.agents.create_version(
        agent_name=agent_name,
        definition=PromptAgentDefinition(
            model=model_deployment_name,
            instructions="You are a helpful assistant that answers general questions",
            tools=[tool],
        )
    )

    print(
        f"Agent created (id: {agent.id}, name: {agent.name}, version: {agent.version})")


def test_conversation():

    # Initialize the client
    project_client = AIProjectClient(
        endpoint=project_endpoint,
        credential=DefaultAzureCredential()
    )

    openai_client = project_client.get_openai_client()

    # Create a conversation with the agent with memory tool enabled
    conversation = openai_client.conversations.create()
    print(f"Created conversation (id: {conversation.id})")

    # Create an agent response to initial user message
    response = openai_client.responses.create(
        input="I prefer dark roast coffee",
        conversation=conversation.id,
        extra_body={"agent": {"name": agent_name, "type": "agent_reference"}},
    )

    print(f"Response output: {response.output_text}")

    # After an inactivity in the conversation, memories will be extracted from the conversation and stored
    print("Waiting for memories to be stored...")
    time.sleep(60)

    # Create a new conversation
    new_conversation = openai_client.conversations.create()
    print(f"Created new conversation (id: {new_conversation.id})")

    # Create an agent response with stored memories
    new_response = openai_client.responses.create(
        input="Please order my usual coffee",
        conversation=new_conversation.id,
        extra_body={"agent": {"name": agent_name, "type": "agent_reference"}},
    )

    print(f"Response output: {new_response.output_text}")


# create_memory_store()
# list_memory_stores()
#test_with_agent()
test_conversation()
