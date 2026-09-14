using AtlasSupply.Application;
using AtlasSupply.Infrastructure;
using AtlasSupply.Mcp;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ListSuppliers>();
builder.Services.AddScoped<GetSupplierById>();
builder.Services.AddScoped<GetDelayedOrders>();
builder.Services.AddScoped<CreateIncident>();
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<AtlasSupplyTools>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "AtlasSupply.Mcp"
}));

app.MapMcp("/mcp");

app.Run();
