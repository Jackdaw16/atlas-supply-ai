# AGENT.md

## Project

**Atlas Supply AI** is a monorepo for an enterprise AI assistant focused on a fictional B2B supply-chain and procurement company.

The product combines:

- Angular frontend for the chat UI.
- .NET backend API.
- .NET MCP server exposing business tools to the AI agent.
- Shared .NET libraries for Domain, Application, and Infrastructure concerns.
- PostgreSQL as the transactional database.
- `pgvector` for vector search / RAG.
- Terraform for infrastructure provisioning.
- Docker for local development and deployment packaging.

The project is intentionally developed incrementally. Do not add large features, frameworks, or abstractions unless the current task requires them.

---

## Repository structure

```text
atlas-supply-ai/
├─ apps/
│  ├─ web/                       # Angular frontend
│  ├─ api/
│  │  └─ AtlasSupply.Api/        # HTTP API
│  └─ mcp/
│     └─ AtlasSupply.Mcp/        # MCP server
│
├─ libs/
│  ├─ AtlasSupply.Domain/        # Entities, value objects, domain rules
│  ├─ AtlasSupply.Application/   # Use cases, commands, queries, interfaces
│  └─ AtlasSupply.Infrastructure/# EF Core, PostgreSQL, external integrations
│
├─ tests/                        # Automated tests
├─ docs/
│  └─ knowledge/                 # Documents used by the RAG pipeline
├─ infra/
│  └─ terraform/                 # Infrastructure as Code
├─ docker/                       # Docker-related assets
├─ AtlasSupply.sln
└─ docker-compose.yml
```

---

## Architectural rules

### Apps vs libraries

`apps/` contains deployable or executable applications.

`libs/` contains reusable code shared by the API, MCP server, and future entry points.

Do not move business logic into `apps/api` or `apps/mcp`.

### Dependency direction

Prefer this dependency flow:

```text
Domain
  ↑
Application
  ↑
Infrastructure
  ↑
API / MCP
```

More precisely:

- `Domain` must not depend on Application, Infrastructure, ASP.NET Core, EF Core, MCP, or cloud SDKs.
- `Application` may depend on Domain.
- `Infrastructure` may depend on Application and Domain.
- `Api` and `Mcp` may depend on Application and Infrastructure.
- API and MCP should not contain duplicated business rules.

### API and MCP

The API and MCP server are two different entry points into the same application logic.

For example, if both need to create an incident:

```text
HTTP endpoint ─┐
               ├─> CreateIncident use case
MCP tool ──────┘
```

Do not implement the same use case independently in both applications.

---

## Domain

The initial business domain is supply chain / procurement.

Expected core concepts include:

- Supplier
- Product
- PurchaseOrder / Order
- OrderItem
- Shipment
- Incident
- QualityInspection

Do not create all entities preemptively. Add them when required by the active feature.

Domain entities should contain meaningful domain behavior where appropriate instead of being only property bags.

Avoid infrastructure-specific annotations in Domain entities unless there is a compelling reason.

---

## Application layer

Application contains use cases and contracts.

Prefer explicit commands and queries such as:

```text
GetSupplierByIdQuery
GetDelayedOrdersQuery
CreateIncidentCommand
GetOrderDetailsQuery
```

Application should define interfaces required from infrastructure, such as repositories or external services.

Keep transport concerns out of Application:

- no HTTP status codes
- no controller-specific types
- no MCP-specific protocol types
- no EF Core DbContext usage

---

## Infrastructure

Infrastructure owns implementation details such as:

- EF Core DbContext
- entity configurations
- migrations
- PostgreSQL repositories
- pgvector integration
- external AI/provider clients
- document ingestion
- embedding persistence

Use EF Core migrations for database schema evolution.

Seed only the minimum deterministic demo data needed to make the project usable after setup.

Do not hide important schema changes behind runtime database creation logic.

---

## Frontend

The frontend lives in `apps/web`.

Technology:

- Angular 22
- standalone APIs
- Angular Material
- Tailwind CSS

Guidelines:

- Keep the first version focused on the chat experience.
- Prefer Angular standalone components.
- Do not introduce NgModules unless required by a dependency.
- Use Angular Material for functional UI components.
- Use Tailwind primarily for layout, spacing, sizing, and lightweight visual composition.
- Avoid duplicating Material styling with large custom CSS blocks.
- Keep API access behind services.
- Do not hardcode backend URLs in components.
- Prefer typed request/response models.

The MVP frontend should remain small:

```text
Chat shell
├─ conversation history
├─ user input
├─ send action
├─ loading/streaming state
└─ error state
```

Do not add dashboards, authentication screens, administration panels, or unrelated pages unless explicitly requested.

---

## AI / Agent architecture

The AI assistant has two fundamentally different information paths.

### Knowledge

Use RAG for relatively stable enterprise knowledge:

```text
docs/knowledge
    ↓
chunking
    ↓
embeddings
    ↓
PostgreSQL + pgvector
    ↓
retrieval
```

Examples:

- supplier policies
- incident escalation procedures
- purchasing rules
- returns policies

### Tools

Use MCP for live business data and actions.

Examples:

```text
get_supplier
get_order
get_delayed_orders
list_suppliers
create_incident
```

Do not use RAG to retrieve live transactional state that belongs in the database.

Do not expose database access directly to the LLM. The model should interact with business capabilities through controlled tools or application services.

---

## MCP rules

MCP tools must:

- have clear names
- have concise descriptions
- use strongly typed input schemas
- return structured data
- validate inputs before executing business operations
- invoke Application use cases rather than duplicating business logic
- avoid exposing implementation details unnecessarily

Tool names should describe business intent, not infrastructure.

Prefer:

```text
create_incident
get_delayed_orders
```

Avoid:

```text
execute_sql
run_query
call_repository_method
```

Any destructive or high-impact tool introduced later should support explicit authorization and, where appropriate, human confirmation.

---

## RAG rules

The initial knowledge source is `docs/knowledge/*.md`.

The ingestion pipeline should be reproducible.

Do not manually insert embeddings directly into the database as an undocumented setup step.

Store enough metadata with chunks to identify their source document.

Responses derived from enterprise knowledge should eventually be able to expose their source references.

Keep chunking and embedding logic isolated so the provider or strategy can be replaced later.

---

## LLM provider abstraction

Do not couple core application logic tightly to one LLM vendor.

Prefer an abstraction such as:

```csharp
public interface ILanguageModel
{
    Task<...> CompleteAsync(...);
}
```

Provider-specific SDK types should remain in Infrastructure or a dedicated integration layer.

The same principle applies to embedding providers.

Do not add multiple providers before the first one works.

---

## Database

Target database:

```text
PostgreSQL + pgvector
```

Local development should work through `docker-compose`.

The database should be reproducible from:

```text
EF Core migrations
+
deterministic seed data
```

Avoid SQLite-specific shortcuts because the deployed target is PostgreSQL.

Use UUIDs or another consistent identifier strategy across the domain.

---

## Terraform

Infrastructure code lives under:

```text
infra/terraform/
```

Expected deployment targets may include:

- Cloudflare Pages for Angular
- Google Cloud Run for .NET services
- GHCR for public container images
- Neon for PostgreSQL/pgvector

Do not assume every service must be provisioned by Terraform if the provider does not support the required operation cleanly.

Prefer small composable Terraform modules over a single oversized file.

Never commit:

- cloud credentials
- API keys
- Terraform state containing secrets
- `.tfvars` files with sensitive values

For CI:

- PRs should run `terraform fmt`, `terraform validate`, and ideally `terraform plan`.
- `terraform apply` should not be automatically executed without explicit approval.

---

## Containers

Every deployable backend service should eventually have its own Dockerfile.

Images should be:

- multi-stage
- minimal
- reproducible
- non-root where practical

Local development should remain possible without requiring cloud infrastructure.

---

## Git strategy

Use short-lived feature branches off `main`.

Examples:

```text
feat/domain-suppliers
feat/postgres-persistence
feat/angular-chat
feat/rag-ingestion
feat/mcp-supplier-tools
feat/terraform-cloud-run
fix/mcp-tool-routing
```

`main` should remain buildable and deployable.

Do not introduce GitFlow or a permanent `develop` branch unless the project grows enough to justify it.

### Commits

Use Conventional Commits.

Examples:

```text
feat(domain): add supplier aggregate
feat(api): expose supplier endpoints
feat(mcp): add delayed orders tool
feat(rag): ingest markdown knowledge
feat(infra): deploy api to cloud run
fix(mcp): prevent duplicate incident creation
docs: add architecture overview
```

Keep commits focused.

---

## Testing

Add tests alongside features, not as a final cleanup phase.

Prioritize:

1. Domain rules
2. Application use cases
3. Repository/integration behavior
4. MCP tool behavior
5. API integration tests
6. Critical frontend behavior

Do not mock everything by default. Use integration tests when persistence or protocol behavior matters.

---

## Security

Never commit secrets.

Configuration must come from environment variables or an appropriate secret manager.

Treat all agent-generated tool arguments as untrusted input.

Validate:

- IDs
- enum values
- lengths
- required fields
- authorization
- business invariants

Do not allow the LLM to bypass Application-layer validation.

Future security work may include:

- OAuth 2.0 / OIDC
- scopes
- RBAC / ABAC
- audit logging
- rate limiting
- tool-level authorization
- human approval for sensitive actions
- prompt-injection defenses
- PII handling

Add these when the product requires them rather than creating empty abstractions up front.

---

## Observability

Use structured logging.

Do not log:

- secrets
- credentials
- raw authorization tokens
- sensitive user data unnecessarily

When agent functionality is implemented, preserve enough telemetry to understand:

```text
user request
→ model decision
→ tool selected
→ tool arguments
→ execution result
→ final response
```

without leaking sensitive content.

---

## Development principles

When modifying this repository:

1. Understand the active feature before editing multiple layers.
2. Make the smallest coherent change that completes the feature.
3. Reuse Application logic between API and MCP.
4. Avoid speculative abstractions.
5. Avoid adding dependencies without a clear need.
6. Prefer explicit code over framework magic.
7. Keep local setup reproducible.
8. Update README or documentation when setup or architecture changes.
9. Preserve the monorepo structure.
10. Leave the project in a buildable state.

---

## Commands

### .NET

From repository root:

```bash
dotnet restore
dotnet build AtlasSupply.sln
dotnet test
```

### Angular

```bash
cd apps/web
npm install
npm start
```

### Local PostgreSQL

From repository root:

```bash
docker compose up -d
```

### EF Core

Run migrations from the appropriate project once persistence is configured.

Prefer commands that explicitly identify the startup and infrastructure projects.

Example shape:

```bash
dotnet ef database update   --project libs/AtlasSupply.Infrastructure   --startup-project apps/api/AtlasSupply.Api
```

Adjust only if the actual project structure requires it.

---

## AI-agent operating rule

Before generating code, inspect the relevant existing files.

Do not regenerate whole applications when a targeted edit is sufficient.

Do not silently rename projects, folders, namespaces, public APIs, environment variables, database tables, or Terraform resources.

When a requested change would materially alter the architecture, explain the proposed change before implementing it.

If the repository and this file disagree, prefer the current repository state for implementation details and update this file when the architectural intent has genuinely changed.
