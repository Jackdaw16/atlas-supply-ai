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

## Knowledge ingestion and retrieval

The initial RAG foundation ingests only the four Markdown sources in `docs/knowledge/`. It stores document and chunk metadata, content hashes, deterministic chunk identities, and OpenAI embeddings in PostgreSQL with pgvector.

The chunker normalizes CRLF/LF newlines, preserves Markdown headings as chunk context, keeps paragraphs together where possible, and limits persisted chunk content to 1,200 characters. Retrieval uses L2 distance, so a smaller `distance` value is a closer match.

### Local development

Set an API key in the shell; never add it to configuration files:

```powershell
$env:OPENAI_API_KEY = "your-key"
$env:ASPNETCORE_ENVIRONMENT = "Development"
docker compose up -d postgres
dotnet ef database update --project libs/AtlasSupply.Infrastructure --startup-project apps/api/AtlasSupply.Api
dotnet run --project apps/api/AtlasSupply.Api
```

`OpenAI:EmbeddingModel` defaults to `text-embedding-3-small`. Set `Knowledge__SourceDirectory` only to a repository-relative Markdown source directory; it defaults to `docs/knowledge`.

The existing API connection configuration uses a different database user from the Docker Compose default. Before the database commands, set `POSTGRESQL_CONNECTION_STRING` in the shell to a connection string that matches the Docker environment you started; do not change checked-in configuration or add credentials to it.

With the URL printed by `dotnet run`, ingest the configured sources and verify retrieval:

```powershell
Invoke-RestMethod -Method Post http://localhost:<port>/api/development/knowledge/ingest
Invoke-RestMethod "http://localhost:<port>/api/development/knowledge/search?query=purchase%20order%20statuses&topK=3"
```

The endpoints exist only in the Development environment. Ingestion skips unchanged normalized document content, updates changed sources, and removes database records for Markdown sources no longer present in the configured directory. Knowledge documents contain stable policy; use existing application queries or MCP for live supplier, purchase order, and incident data.
