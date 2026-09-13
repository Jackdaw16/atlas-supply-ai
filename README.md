# Atlas Supply AI

Enterprise AI assistant for a fictional B2B supply-chain and procurement company.

This repository intentionally starts small. The seed contains only the monorepo structure, .NET 10 project boundaries, local PostgreSQL/pgvector infrastructure, and placeholders for the Angular client, knowledge documents, tests, and Terraform.

## Target architecture

- Angular + Tailwind + Angular Material: chat UI
- .NET 10 API: business API and agent-facing backend
- .NET MCP service: tools exposed to the agent
- PostgreSQL + pgvector: transactional data and vector storage
- RAG: internal company knowledge
- LLM/agent provider: to be selected
- Terraform: deployment infrastructure

## Repository structure

```text
atlas-supply-ai/
├─ apps/
│  ├─ web/                         # Angular app (next step)
│  ├─ api/
│  │  └─ AtlasSupply.Api/          # deployable .NET API
│  └─ mcp/
│     └─ AtlasSupply.Mcp/          # deployable MCP server
├─ libs/
│  ├─ AtlasSupply.Domain/          # domain model
│  ├─ AtlasSupply.Application/     # use cases / application contracts
│  └─ AtlasSupply.Infrastructure/  # persistence and external integrations
├─ tests/                          # test projects as features are added
├─ docs/
│  └─ knowledge/                   # RAG source documents
├─ infra/
│  └─ terraform/                   # IaC (later)
├─ docker/
├─ docker-compose.yml
├─ .env.example
└─ AtlasSupply.sln
```

The convention is deliberate:

- `apps/` contains deployable/executable applications.
- `libs/` contains shared .NET libraries.
- `tests/` contains automated test projects.
- `infra/` contains infrastructure-as-code.
- `docs/knowledge/` is the seed source for the future RAG ingestion pipeline.

## Local database

Copy `.env.example` to `.env` and run:

```bash
docker compose up -d postgres
```

The seed uses the `pgvector/pgvector` PostgreSQL image so the same database can later hold relational data and embeddings.

## Current scope

The seed deliberately does **not** include business entities, EF Core migrations, RAG ingestion, MCP tools, authentication, the agent, or the Angular application yet. Those will be added incrementally after the domain model and first use case are defined.
