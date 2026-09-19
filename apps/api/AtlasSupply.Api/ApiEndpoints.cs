using AtlasSupply.Application;

namespace AtlasSupply.Api;

public static class ApiEndpoints
{
    public static WebApplication MapAtlasSupplyEndpoints(this WebApplication app)
    {
        app.MapOpenApi("/openapi/{documentName}.json");
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Atlas Supply API v1");
        });

        if (app.Environment.IsDevelopment())
        {
            MapDevelopmentEndpoints(app);
        }

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "AtlasSupply.Api"
        }));

        app.MapPost("/api/auth/login", async (
            LoginRequest? request,
            Login login,
            CancellationToken cancellationToken) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["request"] = ["Username and password are required."]
                });
            }

            try
            {
                var result = await login.ExecuteAsync(
                    new LoginInput(request.Username, request.Password),
                    cancellationToken);

                return Results.Ok(new LoginResponse(
                    result.AccessToken,
                    result.ExpiresAtUtc,
                    result.Username,
                    result.Scopes));
            }
            catch (InvalidCredentialsException)
            {
                return Results.Unauthorized();
            }
        })
            .WithName("Login")
            .WithTags("Authentication")
            .Accepts<LoginRequest>("application/json")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/api/suppliers", async (
            ListSuppliers listSuppliers,
            CancellationToken cancellationToken) =>
        {
            var suppliers = await listSuppliers.ExecuteAsync(cancellationToken);
            return Results.Ok(suppliers);
        })
            .WithName("ListSuppliers")
            .WithTags("Suppliers")
            .Produces<IReadOnlyList<SupplierResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/suppliers/{id}", async (
            string id,
            GetSupplierById getSupplierById,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(id, out var supplierId) || supplierId == Guid.Empty)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["id"] = ["Supplier id must be a non-empty GUID."]
                });
            }

            var supplier = await getSupplierById.ExecuteAsync(
                new GetSupplierByIdInput(supplierId),
                cancellationToken);

            return supplier is null
                ? Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Supplier not found.")
                : Results.Ok(supplier);
        })
            .WithName("GetSupplierById")
            .WithTags("Suppliers")
            .Produces<SupplierResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/orders/delayed", async (
            GetDelayedOrders getDelayedOrders,
            CancellationToken cancellationToken) =>
        {
            var delayedOrders = await getDelayedOrders.ExecuteAsync(cancellationToken);
            return Results.Ok(delayedOrders);
        })
            .WithName("GetDelayedOrders")
            .WithTags("Orders")
            .Produces<IReadOnlyList<DelayedOrderResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/chat", async (
            AgentChatRequest request,
            AgentService agentService,
            CancellationToken cancellationToken) =>
        {
            var result = await agentService.ChatAsync(request, cancellationToken);
            return Results.Ok(result);
        })
            .WithName("Chat")
            .WithTags("Chat")
            .Accepts<AgentChatRequest>("application/json")
            .Produces<AgentChatResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/incidents", async (
            CreateIncidentRequest? request,
            CreateIncident createIncident,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateCreateIncidentRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var incident = await createIncident.ExecuteAsync(
                new CreateIncidentInput(
                    request!.Type!.Value,
                    request.Description!,
                    request.SupplierId!.Value,
                    request.PurchaseOrderId),
                cancellationToken);

            return Results.Created($"/api/incidents/{incident.Id}", incident);
        })
            .WithName("CreateIncident")
            .WithTags("Incidents")
            .Accepts<CreateIncidentRequest>("application/json")
            .Produces<CreateIncidentResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static void MapDevelopmentEndpoints(WebApplication app)
    {
        app.MapPost("/api/development/knowledge/ingest", async (
            IKnowledgeIngestionService knowledgeIngestionService,
            CancellationToken cancellationToken) =>
        {
            var result = await knowledgeIngestionService.IngestAsync(cancellationToken);
            return Results.Ok(result);
        })
            .WithName("IngestKnowledge")
            .WithTags("Development")
            .Produces<KnowledgeIngestionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/development/knowledge/search", async (
            string query,
            int? topK,
            IKnowledgeRetrievalService knowledgeRetrievalService,
            CancellationToken cancellationToken) =>
        {
            var results = await knowledgeRetrievalService.SearchAsync(
                new KnowledgeSearchInput(query, topK ?? 5),
                cancellationToken);
            return Results.Ok(results);
        })
            .WithName("SearchKnowledge")
            .WithTags("Development")
            .Produces<IReadOnlyList<KnowledgeSearchResult>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static Dictionary<string, string[]> ValidateCreateIncidentRequest(CreateIncidentRequest? request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request is null)
        {
            errors["request"] = ["Request body is required."];
            return errors;
        }

        if (request.Type is null)
        {
            errors["type"] = ["Incident type is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors["description"] = ["Incident description is required."];
        }

        if (request.SupplierId is null || request.SupplierId == Guid.Empty)
        {
            errors["supplierId"] = ["Supplier id must be a non-empty GUID."];
        }

        if (request.PurchaseOrderId == Guid.Empty)
        {
            errors["purchaseOrderId"] = ["Purchase order id must be a non-empty GUID when supplied."];
        }

        return errors;
    }
}
