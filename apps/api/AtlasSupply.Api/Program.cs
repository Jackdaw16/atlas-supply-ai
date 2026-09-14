using AtlasSupply.Application;
using AtlasSupply.Api;
using AtlasSupply.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ListSuppliers>();
builder.Services.AddScoped<GetSupplierById>();
builder.Services.AddScoped<GetDelayedOrders>();
builder.Services.AddScoped<CreateIncident>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Atlas Supply API v1"));
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "AtlasSupply.Api"
}));

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

app.Run();

static Dictionary<string, string[]> ValidateCreateIncidentRequest(CreateIncidentRequest? request)
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
