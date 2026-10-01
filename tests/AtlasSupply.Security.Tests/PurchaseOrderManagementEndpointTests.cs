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

public sealed class PurchaseOrderManagementEndpointTests
{
    private const string Issuer = "atlas-supply-api-order-tests";
    private const string Audience = "atlas-supply-api-order-tests";
    private const string SigningKey = "order-test-signing-key-with-at-least-thirty-two-bytes";

    [Fact]
    public async Task OrderManagement_ReadEndpointsRequireAuthenticationAndReturnDelayedSignal()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        var unauthenticatedListResponse = await client.GetAsync("/api/orders");
        var unauthenticatedDetailResponse = await client.GetAsync($"/api/orders/{factory.DraftOrderId}");

        Authenticate(client);
        var listResponse = await client.GetAsync("/api/orders");
        var orders = await listResponse.Content.ReadFromJsonAsync<PurchaseOrderResult[]>();
        var delayedOrder = Assert.Single(orders!, order => order.IsDelayed);

        var detailResponse = await client.GetAsync($"/api/orders/{delayedOrder.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<PurchaseOrderResult>();

        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedListResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedDetailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal("Approved", detail!.Status);
        Assert.NotEmpty(detail.Items);
        Assert.True(detail.IsDelayed);
        Assert.True(detail.TotalAmount > 0);
    }

    [Fact]
    public async Task OrderManagement_CreatesUpdatesAndTransitionsDraftOrder()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();
        Authenticate(client);

        var createResponse = await client.PostAsJsonAsync("/api/orders", new
        {
            supplierId = factory.SupplierId,
            items = new[] { new { description = "Packing tape", quantity = 12, unitPrice = 3.25m } }
        });
        var created = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderResult>();

        var updateResponse = await client.PutAsJsonAsync($"/api/orders/{created!.Id}/items", new
        {
            items = new[]
            {
                new { description = "Packing tape", quantity = 18, unitPrice = 3.25m },
                new { description = "Shipping labels", quantity = 4, unitPrice = 5.00m }
            }
        });
        var updated = await updateResponse.Content.ReadFromJsonAsync<PurchaseOrderResult>();
        var submitResponse = await client.PostAsync($"/api/orders/{created.Id}/submit", null);
        var approveResponse = await client.PostAsync($"/api/orders/{created.Id}/approve", null);
        var receiveResponse = await client.PostAsync($"/api/orders/{created.Id}/receive", null);
        var received = await receiveResponse.Content.ReadFromJsonAsync<PurchaseOrderResult>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(2, updated!.Items.Count);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, receiveResponse.StatusCode);
        Assert.Equal("Received", received!.Status);
        Assert.NotNull(received.ReceivedAtUtc);
    }

    [Fact]
    public async Task OrderManagement_ReturnsExpectedValidationNotFoundAndStateResponses()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();
        Authenticate(client);

        var invalidCreateResponse = await client.PostAsJsonAsync("/api/orders", new
        {
            supplierId = Guid.Empty,
            items = new[] { new { description = " ", quantity = 0, unitPrice = 0m } }
        });
        var unknownSupplierResponse = await client.PostAsJsonAsync("/api/orders", new
        {
            supplierId = Guid.NewGuid(),
            items = new[] { new { description = "Valid", quantity = 1, unitPrice = 1m } }
        });
        var emptyDraftUpdateResponse = await client.PutAsJsonAsync($"/api/orders/{factory.DraftOrderId}/items", new { items = Array.Empty<object>() });
        var unknownOrderResponse = await client.PutAsJsonAsync($"/api/orders/{Guid.NewGuid()}/items", new { items = new[] { new { description = "Valid", quantity = 1, unitPrice = 1m } } });
        var invalidTransitionResponse = await client.PostAsync($"/api/orders/{factory.DraftOrderId}/approve", null);
        var cancelResponse = await client.PostAsync($"/api/orders/{factory.DraftOrderId}/cancel", null);
        var repeatCancelResponse = await client.PostAsync($"/api/orders/{factory.DraftOrderId}/cancel", null);
        var cancelled = await repeatCancelResponse.Content.ReadFromJsonAsync<PurchaseOrderResult>();

        Assert.Equal(HttpStatusCode.BadRequest, invalidCreateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownSupplierResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyDraftUpdateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownOrderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidTransitionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatCancelResponse.StatusCode);
        Assert.Equal("Cancelled", cancelled!.Status);
    }

    [Fact]
    public async Task OrderManagement_AcceptsMaximumItemValueAndRejectsOverflowingTotals()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();
        Authenticate(client);

        const decimal maximumUnitPrice = 9_999_999_999_999_999.99m;
        var maximumValueResponse = await client.PostAsJsonAsync("/api/orders", new
        {
            supplierId = factory.SupplierId,
            items = new[] { new { description = "Maximum value item", quantity = 1, unitPrice = maximumUnitPrice } }
        });
        var overflowingTotalResponse = await client.PostAsJsonAsync("/api/orders", new
        {
            supplierId = factory.SupplierId,
            items = Enumerable.Range(0, 4_000)
                .Select(_ => new { description = "Maximum aggregate item", quantity = int.MaxValue, unitPrice = maximumUnitPrice })
                .ToArray()
        });

        Assert.Equal(HttpStatusCode.Created, maximumValueResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overflowingTotalResponse.StatusCode);
    }

    [Fact]
    public async Task OrderManagement_MutationsRequireAuthentication()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();
        var orderId = factory.DraftOrderId;

        var createResponse = await client.PostAsJsonAsync("/api/orders", new { supplierId = factory.SupplierId, items = new[] { new { description = "Valid", quantity = 1, unitPrice = 1m } } });
        var updateResponse = await client.PutAsJsonAsync($"/api/orders/{orderId}/items", new { items = Array.Empty<object>() });
        var submitResponse = await client.PostAsync($"/api/orders/{orderId}/submit", null);
        var approveResponse = await client.PostAsync($"/api/orders/{orderId}/approve", null);
        var receiveResponse = await client.PostAsync($"/api/orders/{orderId}/receive", null);
        var cancelResponse = await client.PostAsync($"/api/orders/{orderId}/cancel", null);

        Assert.Equal(HttpStatusCode.Unauthorized, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, submitResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, approveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, receiveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task OrderManagement_PageEndpointRequiresAuthenticationAndFiltersDelayedOrders()
    {
        using var factory = new OrderApiFactory();
        using var client = factory.CreateClient();

        var unauthorizedResponse = await client.GetAsync("/api/orders/page?pageIndex=0&pageSize=25");
        Authenticate(client);
        var pageResponse = await client.GetAsync("/api/orders/page?pageIndex=0&pageSize=25&status=Approved&isDelayed=true");
        var page = await pageResponse.Content.ReadFromJsonAsync<PagedResult<PurchaseOrderResult>>();
        var invalidResponse = await client.GetAsync("/api/orders/page?pageIndex=0&pageSize=101");
        var maximumPageResponse = await client.GetAsync("/api/orders/page?pageIndex=21474836&pageSize=100");
        var overflowingResponse = await client.GetAsync("/api/orders/page?pageIndex=21474837&pageSize=100");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.NotNull(page);
        Assert.Equal(1, page.TotalCount);
        Assert.All(page.Items, order => Assert.True(order.IsDelayed));
        Assert.Equal(HttpStatusCode.OK, maximumPageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overflowingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
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

    private sealed class OrderApiFactory : WebApplicationFactory<Program>
    {
        private readonly InMemorySupplierRepository supplierRepository;
        private readonly InMemoryPurchaseOrderRepository purchaseOrderRepository;

        internal OrderApiFactory()
        {
            SupplierId = Guid.NewGuid();
            DraftOrderId = Guid.NewGuid();
            supplierRepository = new InMemorySupplierRepository([new Supplier(SupplierId, "Northstar Components", null)]);
            purchaseOrderRepository = new InMemoryPurchaseOrderRepository();

            var draft = new PurchaseOrder(DraftOrderId, SupplierId);
            draft.AddItem(new PurchaseOrderItem(Guid.NewGuid(), "Dock seals", 3, 15m));
            purchaseOrderRepository.Add(draft);

            var delayed = new PurchaseOrder(Guid.NewGuid(), SupplierId);
            delayed.AddItem(new PurchaseOrderItem(Guid.NewGuid(), "Steel mesh", 2, 30m));
            delayed.Submit();
            delayed.Approve();
            purchaseOrderRepository.Add(delayed);
        }

        internal Guid SupplierId { get; }

        internal Guid DraftOrderId { get; }

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
                services.AddSingleton<ISupplierRepository>(supplierRepository);
                services.AddSingleton<IPurchaseOrderRepository>(purchaseOrderRepository);
            });
        }
    }

    private sealed class InMemorySupplierRepository(IReadOnlyList<Supplier> initialSuppliers) : ISupplierRepository
    {
        private readonly Dictionary<Guid, Supplier> suppliers = initialSuppliers.ToDictionary(supplier => supplier.Id);
        public Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Supplier>>([.. suppliers.Values]);
        public Task<PagedResult<Supplier>> PageAsync(SupplierPageRequest request, CancellationToken cancellationToken)
        {
            var matches = suppliers.Values.OrderBy(supplier => supplier.Name).ThenBy(supplier => supplier.Id).ToArray();
            return Task.FromResult(new PagedResult<Supplier>(
                matches.Skip(request.Page.PageIndex * request.Page.PageSize).Take(request.Page.PageSize).ToArray(),
                matches.Length));
        }
        public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(suppliers.GetValueOrDefault(id));
        public Task<Supplier?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(suppliers.GetValueOrDefault(id));
        public Task CreateAsync(Supplier supplier, CancellationToken cancellationToken) { suppliers.Add(supplier.Id, supplier); return Task.CompletedTask; }
        public Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryPurchaseOrderRepository : IPurchaseOrderRepository
    {
        private readonly Dictionary<Guid, PurchaseOrder> purchaseOrders = [];
        public void Add(PurchaseOrder purchaseOrder) => purchaseOrders.Add(purchaseOrder.Id, purchaseOrder);
        public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(purchaseOrders.GetValueOrDefault(id));
        public Task<PurchaseOrder?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(purchaseOrders.GetValueOrDefault(id));
        public Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PurchaseOrder>>([.. purchaseOrders.Values.OrderByDescending(order => order.CreatedAtUtc)]);
        public Task<PagedResult<PurchaseOrder>> PageAsync(PurchaseOrderPageRequest request, CancellationToken cancellationToken)
        {
            var matches = purchaseOrders.Values
                .Where(order => request.Status is null || order.Status == request.Status)
                .Where(order => request.SupplierId is null || order.SupplierId == request.SupplierId)
                .Where(order => request.IsDelayed is null || IsDelayed(order) == request.IsDelayed)
                .OrderByDescending(order => order.CreatedAtUtc)
                .ThenBy(order => order.Id)
                .ToArray();
            return Task.FromResult(new PagedResult<PurchaseOrder>(
                matches.Skip(request.Page.PageIndex * request.Page.PageSize).Take(request.Page.PageSize).ToArray(),
                matches.Length));
        }
        public Task<IReadOnlyList<PurchaseOrder>> ListOutstandingApprovedAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PurchaseOrder>>([.. purchaseOrders.Values.Where(order => order.Status == PurchaseOrderStatus.Approved && order.ApprovedAtUtc is not null && order.ReceivedAtUtc is null)]);
        public Task CreateAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken) { Add(purchaseOrder); return Task.CompletedTask; }
        public Task UpdateAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken) => Task.CompletedTask;

        private static bool IsDelayed(PurchaseOrder order) =>
            order.Status == PurchaseOrderStatus.Approved && order.ApprovedAtUtc is not null && order.ReceivedAtUtc is null;
    }
}
