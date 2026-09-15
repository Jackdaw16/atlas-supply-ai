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

app.MapAtlasSupplyEndpoints();

app.Run();
