using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using AtlasSupply.Application;
using AtlasSupply.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AtlasSupply.Security.Tests;

public sealed class IncidentManagementEndpointTests
{
    private const string Issuer = "atlas-supply-api-incident-tests";
    private const string Audience = "atlas-supply-api-incident-tests";
    private const string SigningKey = "incident-test-signing-key-with-at-least-thirty-two-bytes";

    [Fact]
    public async Task IncidentManagement_ReadEndpointsRequireAuthenticationAndReturnIncidentFields()
    {
        using var factory = new IncidentApiFactory();
        using var client = factory.CreateClient();

        var unauthenticatedListResponse = await client.GetAsync("/api/incidents");
        var unauthenticatedDetailResponse = await client.GetAsync($"/api/incidents/{factory.OpenIncidentId}");

        Authenticate(client);
        var listResponse = await client.GetAsync("/api/incidents");
        var incidents = await listResponse.Content.ReadFromJsonAsync<IncidentResult[]>();
        var detailResponse = await client.GetAsync($"/api/incidents/{factory.OpenIncidentId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<IncidentResult>();

        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedListResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedDetailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Contains(incidents!, incident => incident.Id == factory.OpenIncidentId);
        Assert.Equal("Open", detail!.Status);
        Assert.Equal("Delay", detail.Type);
        Assert.Null(detail.ResolvedAtUtc);
        Assert.Null(detail.ClosedAtUtc);
    }

    [Fact]
    public async Task IncidentManagement_CreatesUpdatesAndResolvesIncident()
    {
        using var factory = new IncidentApiFactory();
        using var client = factory.CreateClient();
        Authenticate(client);

        var createResponse = await client.PostAsJsonAsync("/api/incidents", new
        {
            type = "QualityIssue",
            description = "Coating sample requires inspection.",
            supplierId = factory.SupplierId,
            purchaseOrderId = factory.PurchaseOrderId
        });
        var created = await createResponse.Content.ReadFromJsonAsync<CreateIncidentResult>();
        var updateResponse = await client.PutAsJsonAsync($"/api/incidents/{created!.Id}/description", new { description = "Coating sample requires laboratory inspection." });
        var updated = await updateResponse.Content.ReadFromJsonAsync<IncidentResult>();
        var resolveResponse = await client.PostAsync($"/api/incidents/{created.Id}/resolve", null);
        var resolved = await resolveResponse.Content.ReadFromJsonAsync<IncidentResult>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Coating sample requires laboratory inspection.", updated!.Description);
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
        Assert.Equal("Resolved", resolved!.Status);
        Assert.NotNull(resolved.ResolvedAtUtc);
    }

    [Fact]
    public async Task IncidentManagement_EnforcesSupplierOrderRelationshipAndDomainTransitions()
    {
        using var factory = new IncidentApiFactory();
        using var client = factory.CreateClient();
        Authenticate(client);

        var mismatchedOrderResponse = await client.PostAsJsonAsync("/api/incidents", new
        {
            type = IncidentType.Delay,
            description = "Supplier mismatch.",
            supplierId = factory.SupplierId,
            purchaseOrderId = factory.OtherSupplierPurchaseOrderId
        });
        var unknownIncidentResponse = await client.PutAsJsonAsync($"/api/incidents/{Guid.NewGuid()}/description", new { description = "Valid description." });
        var resolveResponse = await client.PostAsync($"/api/incidents/{factory.ResolvedIncidentId}/resolve", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, mismatchedOrderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownIncidentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resolveResponse.StatusCode);
    }

    [Fact]
    public async Task IncidentManagement_MutationsRequireAuthentication()
    {
        using var factory = new IncidentApiFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/incidents", new { type = IncidentType.Other, description = "Valid description.", supplierId = factory.SupplierId });
        var updateResponse = await client.PutAsJsonAsync($"/api/incidents/{factory.OpenIncidentId}/description", new { description = "Valid description." });
        var resolveResponse = await client.PostAsync($"/api/incidents/{factory.OpenIncidentId}/resolve", null);

        Assert.Equal(HttpStatusCode.Unauthorized, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, resolveResponse.StatusCode);
    }

    private static void Authenticate(HttpClient client)
    {
        var now = DateTimeOffset.UtcNow;
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString("D")), new Claim("name", "test-user")],
            notBefore: now.AddMinutes(-1).UtcDateTime,
            expires: now.AddMinutes(5).UtcDateTime,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private sealed class IncidentApiFactory : WebApplicationFactory<Program>
    {
        private readonly InMemorySupplierRepository supplierRepository;
        private readonly InMemoryPurchaseOrderRepository purchaseOrderRepository;
        private readonly InMemoryIncidentRepository incidentRepository;

        internal IncidentApiFactory()
        {
            SupplierId = Guid.NewGuid();
            var otherSupplierId = Guid.NewGuid();
            PurchaseOrderId = Guid.NewGuid();
            OtherSupplierPurchaseOrderId = Guid.NewGuid();
            OpenIncidentId = Guid.NewGuid();
            ResolvedIncidentId = Guid.NewGuid();
            supplierRepository = new InMemorySupplierRepository([
                new Supplier(SupplierId, "Northstar Components", null),
                new Supplier(otherSupplierId, "Other Supplier", null)
            ]);
            purchaseOrderRepository = new InMemoryPurchaseOrderRepository([
                new PurchaseOrder(PurchaseOrderId, SupplierId),
                new PurchaseOrder(OtherSupplierPurchaseOrderId, otherSupplierId)
            ]);
            var openIncident = new Incident(OpenIncidentId, IncidentType.Delay, "Inbound delivery missed its booking slot.", SupplierId, PurchaseOrderId);
            var resolvedIncident = new Incident(ResolvedIncidentId, IncidentType.Other, "Resolved operational issue.", SupplierId);
            resolvedIncident.Resolve();
            incidentRepository = new InMemoryIncidentRepository([openIncident, resolvedIncident]);
        }

        internal Guid SupplierId { get; }
        internal Guid PurchaseOrderId { get; }
        internal Guid OtherSupplierPurchaseOrderId { get; }
        internal Guid OpenIncidentId { get; }
        internal Guid ResolvedIncidentId { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Database=atlas_supply_tests",
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:AccessTokenMinutes"] = "15",
                ["JWT_SIGNING_KEY"] = SigningKey
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISupplierRepository>();
                services.RemoveAll<IPurchaseOrderRepository>();
                services.RemoveAll<IIncidentRepository>();
                services.AddSingleton<ISupplierRepository>(supplierRepository);
                services.AddSingleton<IPurchaseOrderRepository>(purchaseOrderRepository);
                services.AddSingleton<IIncidentRepository>(incidentRepository);
            });
        }
    }

    private sealed class InMemorySupplierRepository(IReadOnlyList<Supplier> suppliers) : ISupplierRepository
    {
        private readonly Dictionary<Guid, Supplier> suppliers = suppliers.ToDictionary(supplier => supplier.Id);
        public Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Supplier>>([.. suppliers.Values]);
        public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(suppliers.GetValueOrDefault(id));
        public Task<Supplier?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(suppliers.GetValueOrDefault(id));
        public Task CreateAsync(Supplier supplier, CancellationToken cancellationToken) { suppliers.Add(supplier.Id, supplier); return Task.CompletedTask; }
        public Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryPurchaseOrderRepository(IReadOnlyList<PurchaseOrder> purchaseOrders) : IPurchaseOrderRepository
    {
        private readonly Dictionary<Guid, PurchaseOrder> purchaseOrders = purchaseOrders.ToDictionary(purchaseOrder => purchaseOrder.Id);
        public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(purchaseOrders.GetValueOrDefault(id));
        public Task<PurchaseOrder?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(purchaseOrders.GetValueOrDefault(id));
        public Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PurchaseOrder>>([.. purchaseOrders.Values]);
        public Task<IReadOnlyList<PurchaseOrder>> ListOutstandingApprovedAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PurchaseOrder>>([]);
        public Task CreateAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken) { purchaseOrders.Add(purchaseOrder.Id, purchaseOrder); return Task.CompletedTask; }
        public Task UpdateAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryIncidentRepository(IReadOnlyList<Incident> incidents) : IIncidentRepository
    {
        private readonly Dictionary<Guid, Incident> incidents = incidents.ToDictionary(incident => incident.Id);
        public Task AddAsync(Incident incident, CancellationToken cancellationToken) { incidents.Add(incident.Id, incident); return Task.CompletedTask; }
        public Task<IReadOnlyList<Incident>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Incident>>([.. incidents.Values.OrderByDescending(incident => incident.CreatedAtUtc)]);
        public Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(incidents.GetValueOrDefault(id));
        public Task<Incident?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(incidents.GetValueOrDefault(id));
        public Task UpdateAsync(Incident incident, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
