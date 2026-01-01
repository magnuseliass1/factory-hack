# List knowledge sources by name and type
import json
import os

import requests
from azure.core.credentials import AzureKeyCredential
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from azure.search.documents.indexes import SearchIndexClient
from azure.search.documents.indexes.models import (
    AzureBlobKnowledgeSource,
    AzureBlobKnowledgeSourceParameters,
    AzureOpenAIVectorizerParameters,
    KnowledgeBase,
    KnowledgeBaseAzureOpenAIModel,
    KnowledgeRetrievalLowReasoningEffort,
    KnowledgeRetrievalOutputMode,
    KnowledgeSourceAzureOpenAIVectorizer,
    KnowledgeSourceContentExtractionMode,
    KnowledgeSourceIngestionParameters,
    KnowledgeSourceReference,
)
from azure.search.documents.knowledgebases import KnowledgeBaseRetrievalClient
from azure.search.documents.knowledgebases.models import (
    KnowledgeBaseMessage,
    KnowledgeBaseMessageTextContent,
    KnowledgeBaseRetrievalRequest,
    SearchIndexKnowledgeSourceParams,
)

search_endpoint = os.environ.get("SEARCH_SERVICE_ENDPOINT")
storage_connection_string = os.environ.get("AZURE_STORAGE_CONNECTION_STRING")
search_key = os.environ.get("SEARCH_ADMIN_KEY")
knowledge_source_endpoint = f"{search_endpoint}/knowledgesources"
knowledge_base_endpoint = f"{search_endpoint}/knowledgebases"
params = {"api-version": "2025-11-01-preview", "$select": "name, kind"}
headers = {"api-key": search_key}
knowledge_source_name = "machine-wiki-blob-ks"
openai_endpoint = os.environ.get("AZURE_OPENAI_ENDPOINT")
openai_key = os.environ.get("AZURE_OPENAI_KEY")
knowledge_base_name = 'machine-kb'


def get_all_knowledge_sources():
    response = requests.get(
        f"{knowledge_source_endpoint}", params=params, headers=headers)
    print(json.dumps(response.json(), indent=2))


def get_knowledge_source(name=knowledge_source_name):
    response = requests.get(f"{knowledge_source_endpoint}/{name}",
                            params=params, headers=headers)
    print(json.dumps(response.json(), indent=2))


def get_index_status():
    response = requests.get(
        f"{knowledge_source_endpoint}/{knowledge_source_name}/status", params=params, headers=headers)
    print(json.dumps(response.json(), indent=2))


def delete_knowledge_source(name=knowledge_source_name):
    index_client = SearchIndexClient(
        endpoint=search_endpoint, credential=AzureKeyCredential(search_key))
    index_client.delete_knowledge_source(knowledge_source_name)
    print("Knowledge source deleted successfully.")


def create_knowledge_source():
    index_client = SearchIndexClient(
        endpoint=search_endpoint, credential=AzureKeyCredential(search_key))

    knowledge_source = AzureBlobKnowledgeSource(
        name=knowledge_source_name,
        description="This knowledge source pulls from a blob storage container.",
        encryption_key=None,
        azure_blob_parameters=AzureBlobKnowledgeSourceParameters(
            connection_string=storage_connection_string,
            container_name="machine-wiki",
            folder_path=None,
            is_adls_gen2=False,
            ingestion_parameters=KnowledgeSourceIngestionParameters(
                identity=None,
                disable_image_verbalization=False,
                chat_completion_model=KnowledgeBaseAzureOpenAIModel(
                    azure_open_ai_parameters=AzureOpenAIVectorizerParameters(
                        resource_url=os.environ.get("AZURE_OPENAI_ENDPOINT"),
                        deployment_name="gpt-4.1",
                        api_key=os.environ.get("AZURE_OPENAI_KEY"),
                        model_name="gpt-4.1"
                    )
                ),
                embedding_model=KnowledgeSourceAzureOpenAIVectorizer(
                    azure_open_ai_parameters=AzureOpenAIVectorizerParameters(
                        resource_url=os.environ.get("AZURE_OPENAI_ENDPOINT"),
                        deployment_name="text-embedding-ada-002",
                        api_key=os.environ.get("AZURE_OPENAI_KEY"),
                        model_name="text-embedding-ada-002"
                    )
                ),
                content_extraction_mode=KnowledgeSourceContentExtractionMode.MINIMAL,
                ingestion_schedule=None,
                ingestion_permission_options=None
            )
        )
    )

    index_client.create_or_update_knowledge_source(knowledge_source)
    print(
        f"Knowledge source '{knowledge_source.name}' created or updated successfully.")


def create_knowledge_base():
    index_client = SearchIndexClient(
        endpoint=search_endpoint, credential=AzureKeyCredential(search_key))

    aoai_params = AzureOpenAIVectorizerParameters(
        resource_url=openai_endpoint,
        api_key=openai_key,
        deployment_name="gpt-4.1",
        model_name="gpt-4.1",
    )

    knowledge_base = KnowledgeBase(
        name="machine-kb",
        description="This knowledge base handles questions about common issues with manufacturing machines",
        retrieval_instructions=f"Use the {knowledge_source_name} to query potential root causes for problems by machine type",
        answer_instructions="Provide a single sentence for the likely cause of the issue based on the retrieved documents.",
        output_mode=KnowledgeRetrievalOutputMode.ANSWER_SYNTHESIS,
        knowledge_sources=[
            KnowledgeSourceReference(name=knowledge_source_name)
        ],
        models=[KnowledgeBaseAzureOpenAIModel(
            azure_open_ai_parameters=aoai_params)],
        encryption_key=None,
        retrieval_reasoning_effort=KnowledgeRetrievalLowReasoningEffort,
    )

    index_client.create_or_update_knowledge_base(knowledge_base)
    print(
        f"Knowledge base '{knowledge_base.name}' created or updated successfully.")


def get_knowledge_base(name=knowledge_base_name):
    response = requests.get(
        f'{knowledge_base_endpoint}/{knowledge_base_name}', params=params, headers=headers)
    print(json.dumps(response.json(), indent=2))


def test_knowledge_base():
    kb_client = KnowledgeBaseRetrievalClient(
        endpoint=search_endpoint, knowledge_base_name=knowledge_base_name, credential=AzureKeyCredential(search_key))

    request = KnowledgeBaseRetrievalRequest(
        messages=[
            KnowledgeBaseMessage(
                role="assistant",
                content=[KnowledgeBaseMessageTextContent(
                    text="What can be the potential issue if curing_temperature is above 178°C")]
            ),
        ],
        knowledge_source_params=[
            SearchIndexKnowledgeSourceParams(
                knowledge_source_name=knowledge_source_name,
                include_references=True,
                include_reference_source_data=True,
                always_query_source=False,
            )
        ],
        include_activity=True,
    )

    result = kb_client.retrieve(request)
    print(result.response[0].content[0].text)


def create_project_connection():
    # Provide connection details
    credential = DefaultAzureCredential()
    # e.g. /subscriptions/{subscription}/resourceGroups/{resource_group}/providers/Microsoft.MachineLearningServices/workspaces/{account_name}/projects/{project_name}
    project_resource_id = "/subscriptions/b2f949ca-aba6-4dce-a438-548eeedf8c8f/resourceGroups/rg-tire-factory-hack/providers/Microsoft.CognitiveServices/accounts/msagthack-aifoundry-2arp3i4mctbwy/projects/msagthack-aiproject-2arp3i4mctbwy"
    project_connection_name = "machine-wiki-connection"

    # This endpoint enables the MCP connection between the agent and knowledge base
    mcp_endpoint = f"{search_endpoint}knowledgebases/{knowledge_base_name}/mcp?api-version=2025-11-01-preview"

    # Get bearer token for authentication
    bearer_token_provider = get_bearer_token_provider(
        credential, "https://management.azure.com/.default")
    headers = {
        "Authorization": f"Bearer {bearer_token_provider()}",
    }

    # Create project connection
    response = requests.put(
        f"https://management.azure.com{project_resource_id}/connections/{project_connection_name}?api-version=2025-10-01-preview",
        headers=headers,
        json={
            "name": project_connection_name,
            "type": "Microsoft.MachineLearningServices/workspaces/connections",
            "properties": {
                "authType": "ProjectManagedIdentity",
                "category": "RemoteTool",
                "target": mcp_endpoint,
                "isSharedToAll": True,
                "audience": "https://search.azure.com/",
                "metadata": {"ApiType": "Azure"}
            }
        }
    )

    response.raise_for_status()
    print(
        f"Connection '{project_connection_name}' created or updated successfully.")

# create_knowledge_base()
# get_knowledge_base()
# create_knowledge_source()
# delete_knowledge_source()
# get_knowledge_source()
# get_index_status()
# get_all_knowledge_sources()


# get_knowledge_source("ks-azureblob-224")
# test_knowledge_base()
create_project_connection()
