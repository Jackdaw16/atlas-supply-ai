using AtlasSupply.Application;
using AtlasSupply.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ListSuppliers>();
builder.Services.AddScoped<GetSupplierById>();
builder.Services.AddScoped<GetDelayedOrders>();
builder.Services.AddScoped<CreateIncident>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "AtlasSupply.Api"
}));

app.Run();
