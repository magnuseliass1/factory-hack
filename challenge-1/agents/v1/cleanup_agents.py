import asyncio
import os
import sys
import argparse
from typing import List, Any

from azure.identity.aio import AzureCliCredential
from azure.ai.projects.aio import AIProjectClient

async def list_agents(client) -> List[Any]:
    # Support both possible SDK method names
    try:
        return [a async for a in client.agents.list_agents()]
    except AttributeError:
        return [a async for a in client.agents.list()]

async def delete_agent(client, agent_id: str):
    # Support both possible SDK method names
    try:
        await client.agents.delete_agent(agent_id)
    except AttributeError:
        await client.agents.delete(agent_id)

async def main():
    parser = argparse.ArgumentParser(description="Cleanup Azure AI agents in the current project")
    parser.add_argument("--endpoint", default=os.environ.get("AZURE_AI_PROJECT_ENDPOINT") or os.environ.get("AZURE_AI_PROJECT_ENDPOINT"), help="Project endpoint (overrides env)")
    parser.add_argument("--name", help="Delete only agents with this exact name")
    parser.add_argument("--name-prefix", help="Delete agents whose name starts with this prefix")
    parser.add_argument("--all", action="store_true", help="Delete all agents")
    parser.add_argument("--confirm", action="store_true", help="Actually delete (otherwise dry-run)")
    args = parser.parse_args()

    if not args.endpoint:
        print("Error: missing --endpoint or AZURE_AI_PROJECT_ENDPOINT")
        sys.exit(2)

    if not (args.all or args.name or args.name_prefix):
        print("Specify one of --all, --name, or --name-prefix")
        sys.exit(2)

    async with AzureCliCredential() as cred:
        async with AIProjectClient(endpoint=args.endpoint, credential=cred) as client:
            agents = await list_agents(client)
            def matches(a) -> bool:
                nm = getattr(a, "name", None) or ""
                if args.all:
                    return True
                if args.name and nm == args.name:
                    return True
                if args.name_prefix and nm.startswith(args.name_prefix):
                    return True
                return False

            victims = [a for a in agents if matches(a)]
            if not victims:
                print("No agents matched the selection.")
                return

            print(f"Matched {len(victims)} agent(s):")
            for a in victims:
                print(f"- {getattr(a, 'id', '?')}  {getattr(a, 'name', '')}")

            if not args.confirm:
                print("\nDry-run only. Re-run with --confirm to delete.")
                return

            for a in victims:
                aid = getattr(a, "id", None)
                if not aid:
                    continue
                print(f"Deleting {aid} ({getattr(a, 'name', '')}) ...")
                await delete_agent(client, aid)

            print("Done.")

if __name__ == "__main__":
    asyncio.run(main())