using AtlasSupply.Application;
using System.Net.Mail;

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

        app.MapPost("/api/suppliers", async (
            SupplierRequest? request,
            CreateSupplier createSupplier,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateSupplierRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var supplier = await createSupplier.ExecuteAsync(
                new CreateSupplierInput(request!.Name!, request.ContactEmail),
                cancellationToken);

            return Results.Created($"/api/suppliers/{supplier.Id}", supplier);
        })
            .WithName("CreateSupplier")
            .WithTags("Suppliers")
            .RequireAuthorization()
            .Accepts<SupplierRequest>("application/json")
            .Produces<SupplierResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapPut("/api/suppliers/{id}", async (
            string id,
            SupplierRequest? request,
            UpdateSupplier updateSupplier,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseSupplierId(id, out var supplierId))
            {
                return InvalidSupplierId();
            }

            var validationErrors = ValidateSupplierRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var supplier = await updateSupplier.ExecuteAsync(
                new UpdateSupplierInput(supplierId, request!.Name!, request.ContactEmail),
                cancellationToken);

            return supplier is null ? SupplierNotFound() : Results.Ok(supplier);
        })
            .WithName("UpdateSupplier")
            .WithTags("Suppliers")
            .RequireAuthorization()
            .Accepts<SupplierRequest>("application/json")
            .Produces<SupplierResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/suppliers/{id}/activate", async (
            string id,
            ActivateSupplier activateSupplier,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseSupplierId(id, out var supplierId))
            {
                return InvalidSupplierId();
            }

            var supplier = await activateSupplier.ExecuteAsync(
                new ActivateSupplierInput(supplierId),
                cancellationToken);

            return supplier is null ? SupplierNotFound() : Results.Ok(supplier);
        })
            .WithName("ActivateSupplier")
            .WithTags("Suppliers")
            .RequireAuthorization()
            .Produces<SupplierResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/suppliers/{id}/deactivate", async (
            string id,
            DeactivateSupplier deactivateSupplier,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseSupplierId(id, out var supplierId))
            {
                return InvalidSupplierId();
            }

            var supplier = await deactivateSupplier.ExecuteAsync(
                new DeactivateSupplierInput(supplierId),
                cancellationToken);

            return supplier is null ? SupplierNotFound() : Results.Ok(supplier);
        })
            .WithName("DeactivateSupplier")
            .WithTags("Suppliers")
            .RequireAuthorization()
            .Produces<SupplierResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/orders", async (
            ListPurchaseOrders listPurchaseOrders,
            CancellationToken cancellationToken) =>
        {
            var purchaseOrders = await listPurchaseOrders.ExecuteAsync(cancellationToken);
            return Results.Ok(purchaseOrders);
        })
            .WithName("ListPurchaseOrders")
            .WithTags("Orders")
            .RequireAuthorization()
            .Produces<IReadOnlyList<PurchaseOrderResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapGet("/api/orders/{id}", async (
            string id,
            GetPurchaseOrderById getPurchaseOrderById,
            CancellationToken cancellationToken) =>
        {
            if (!TryParsePurchaseOrderId(id, out var purchaseOrderId))
            {
                return InvalidPurchaseOrderId();
            }

            var purchaseOrder = await getPurchaseOrderById.ExecuteAsync(
                new GetPurchaseOrderByIdInput(purchaseOrderId),
                cancellationToken);
            return purchaseOrder is null ? PurchaseOrderNotFound() : Results.Ok(purchaseOrder);
        })
            .WithName("GetPurchaseOrderById")
            .WithTags("Orders")
            .RequireAuthorization()
            .Produces<PurchaseOrderResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/orders", async (
            CreatePurchaseOrderRequest? request,
            CreatePurchaseOrder createPurchaseOrder,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateCreatePurchaseOrderRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var purchaseOrder = await createPurchaseOrder.ExecuteAsync(
                new CreatePurchaseOrderInput(request!.SupplierId!.Value, ToItemInputs(request.Items!)),
                cancellationToken);
            return purchaseOrder is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Supplier not found.")
                : Results.Created($"/api/orders/{purchaseOrder.Id}", purchaseOrder);
        })
            .WithName("CreatePurchaseOrder")
            .WithTags("Orders")
            .RequireAuthorization()
            .Accepts<CreatePurchaseOrderRequest>("application/json")
            .Produces<PurchaseOrderResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapPut("/api/orders/{id}/items", async (
            string id,
            UpdatePurchaseOrderItemsRequest? request,
            UpdatePurchaseOrderDraftItems updatePurchaseOrderDraftItems,
            CancellationToken cancellationToken) =>
        {
            if (!TryParsePurchaseOrderId(id, out var purchaseOrderId))
            {
                return InvalidPurchaseOrderId();
            }

            var validationErrors = ValidateUpdatePurchaseOrderItemsRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors);
            }

            var purchaseOrder = await updatePurchaseOrderDraftItems.ExecuteAsync(
                new UpdatePurchaseOrderDraftItemsInput(purchaseOrderId, ToItemInputs(request!.Items!)),
                cancellationToken);
            return purchaseOrder is null ? PurchaseOrderNotFound() : Results.Ok(purchaseOrder);
        })
            .WithName("UpdatePurchaseOrderDraftItems")
            .WithTags("Orders")
            .RequireAuthorization()
            .Accepts<UpdatePurchaseOrderItemsRequest>("application/json")
            .Produces<PurchaseOrderResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/orders/{id}/submit", (string id, SubmitPurchaseOrder submitPurchaseOrder, CancellationToken cancellationToken) =>
            ExecutePurchaseOrderTransition(id, purchaseOrderId => submitPurchaseOrder.ExecuteAsync(new SubmitPurchaseOrderInput(purchaseOrderId), cancellationToken)))
            .WithName("SubmitPurchaseOrder")
            .WithTags("Orders")
            .RequireAuthorization()
            .Produces<PurchaseOrderResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/orders/{id}/approve", (string id, ApprovePurchaseOrder approvePurchaseOrder, CancellationToken cancellationToken) =>
            ExecutePurchaseOrderTransition(id, purchaseOrderId => approvePurchaseOrder.ExecuteAsync(new ApprovePurchaseOrderInput(purchaseOrderId), cancellationToken)))
            .WithName("ApprovePurchaseOrder")
            .WithTags("Orders")
            .RequireAuthorization()
            .Produces<PurchaseOrderResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/orders/{id}/receive", (string id, MarkPurchaseOrderReceived markPurchaseOrderReceived, CancellationToken cancellationToken) =>
            ExecutePurchaseOrderTransition(id, purchaseOrderId => markPurchaseOrderReceived.ExecuteAsync(new MarkPurchaseOrderReceivedInput(purchaseOrderId), cancellationToken)))
            .WithName("MarkPurchaseOrderReceived")
            .WithTags("Orders")
            .RequireAuthorization()
            .Produces<PurchaseOrderResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        app.MapPost("/api/orders/{id}/cancel", (string id, CancelPurchaseOrder cancelPurchaseOrder, CancellationToken cancellationToken) =>
            ExecutePurchaseOrderTransition(id, purchaseOrderId => cancelPurchaseOrder.ExecuteAsync(new CancelPurchaseOrderInput(purchaseOrderId), cancellationToken)))
            .WithName("CancelPurchaseOrder")
            .WithTags("Orders")
            .RequireAuthorization()
            .Produces<PurchaseOrderResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
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
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userId = Guid.TryParse(httpContext.User.FindFirst("sub")?.Value, out var subjectId)
                ? subjectId
                : throw new InvalidOperationException("Validated token subject is invalid.");
            var username = httpContext.User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidOperationException("Validated token username is missing.");
            }

            var authorizationContext = AgentAuthorizationContext.FromValidatedScopeClaimValues(
                userId,
                username,
                httpContext.User.FindAll(AgentAuthorizationContext.ScopeClaimType)
                    .Select(static claim => claim.Value));
            var result = await agentService.ChatAsync(request, authorizationContext, cancellationToken);
            return Results.Ok(result);
        })
            .WithName("Chat")
            .WithTags("Chat")
            .RequireAuthorization()
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

    private static bool TryParseSupplierId(string id, out Guid supplierId) =>
        Guid.TryParse(id, out supplierId) && supplierId != Guid.Empty;

    private static bool TryParsePurchaseOrderId(string id, out Guid purchaseOrderId) =>
        Guid.TryParse(id, out purchaseOrderId) && purchaseOrderId != Guid.Empty;

    private static IResult InvalidSupplierId() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["id"] = ["Supplier id must be a non-empty GUID."]
    });

    private static IResult SupplierNotFound() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Supplier not found.");

    private static IResult InvalidPurchaseOrderId() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["id"] = ["Purchase order id must be a non-empty GUID."]
    });

    private static IResult PurchaseOrderNotFound() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Purchase order not found.");

    private static async Task<IResult> ExecutePurchaseOrderTransition(
        string id,
        Func<Guid, Task<PurchaseOrderResult?>> execute)
    {
        if (!TryParsePurchaseOrderId(id, out var purchaseOrderId))
        {
            return InvalidPurchaseOrderId();
        }

        var purchaseOrder = await execute(purchaseOrderId);
        return purchaseOrder is null ? PurchaseOrderNotFound() : Results.Ok(purchaseOrder);
    }

    private static Dictionary<string, string[]> ValidateCreatePurchaseOrderRequest(CreatePurchaseOrderRequest? request)
    {
        var errors = ValidatePurchaseOrderItemsRequest(request?.Items, requireAtLeastOne: true);
        if (request is null)
        {
            errors["request"] = ["Request body is required."];
            return errors;
        }

        if (request.SupplierId is null || request.SupplierId == Guid.Empty)
        {
            errors["supplierId"] = ["Supplier id must be a non-empty GUID."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateUpdatePurchaseOrderItemsRequest(UpdatePurchaseOrderItemsRequest? request)
    {
        var errors = ValidatePurchaseOrderItemsRequest(request?.Items, requireAtLeastOne: true);
        if (request is null)
        {
            errors["request"] = ["Request body is required."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidatePurchaseOrderItemsRequest(
        IReadOnlyList<PurchaseOrderItemRequest>? items,
        bool requireAtLeastOne)
    {
        var errors = new Dictionary<string, string[]>();
        if (items is null)
        {
            errors["items"] = ["Purchase order items are required."];
            return errors;
        }

        if (requireAtLeastOne && items.Count == 0)
        {
            errors["items"] = ["Purchase order requires at least one item."];
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item is null)
            {
                errors[$"items[{index}]"] = ["Purchase order item is required."];
                continue;
            }

            var description = item.Description?.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                errors[$"items[{index}].description"] = ["Item description is required."];
            }
            else if (description.Length > 500)
            {
                errors[$"items[{index}].description"] = ["Item description cannot exceed 500 characters."];
            }

            if (item.Quantity is null || item.Quantity <= 0)
            {
                errors[$"items[{index}].quantity"] = ["Item quantity must be greater than zero."];
            }

            if (item.UnitPrice is null || item.UnitPrice <= 0)
            {
                errors[$"items[{index}].unitPrice"] = ["Item unit price must be greater than zero."];
            }
            else if (decimal.Round(item.UnitPrice.Value, 2) != item.UnitPrice.Value || item.UnitPrice > 9_999_999_999_999_999.99m)
            {
                errors[$"items[{index}].unitPrice"] = ["Item unit price must fit a monetary value with up to 16 integral digits and 2 decimal places."];
            }
        }

        if (errors.Count == 0 && !PurchaseOrderValidation.HasSafeTotal(ToItemInputs(items)))
        {
            errors["items"] = ["Purchase order total exceeds the supported monetary range."];
        }

        return errors;
    }

    private static IReadOnlyList<PurchaseOrderItemInput> ToItemInputs(IReadOnlyList<PurchaseOrderItemRequest> items) =>
        items.Select(static item => new PurchaseOrderItemInput(
            item.Description!.Trim(),
            item.Quantity!.Value,
            item.UnitPrice!.Value)).ToArray();

    private static Dictionary<string, string[]> ValidateSupplierRequest(SupplierRequest? request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request is null)
        {
            errors["request"] = ["Request body is required."];
            return errors;
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["Supplier name is required."];
        }
        else if (name.Length > 200)
        {
            errors["name"] = ["Supplier name cannot exceed 200 characters."];
        }

        var contactEmail = request.ContactEmail?.Trim();
        if (!string.IsNullOrWhiteSpace(contactEmail))
        {
            if (contactEmail.Length > 320)
            {
                errors["contactEmail"] = ["Contact email cannot exceed 320 characters."];
            }
            else if (!IsValidContactEmail(contactEmail))
            {
                errors["contactEmail"] = ["Contact email is invalid."];
            }
        }

        return errors;
    }

    private static bool IsValidContactEmail(string contactEmail)
    {
        try
        {
            var emailAddress = new MailAddress(contactEmail);
            return string.Equals(emailAddress.Address, contactEmail, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
