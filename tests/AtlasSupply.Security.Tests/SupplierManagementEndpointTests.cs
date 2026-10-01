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

public sealed class SupplierManagementEndpointTests
{
    private const string Issuer = "atlas-supply-api-tests";
    private const string Audience = "atlas-supply-api-tests";
    private const string SigningKey = "test-signing-key-with-at-least-thirty-two-bytes";

    [Fact]
    public async Task SupplierManagement_CreateUpdateDeactivateAndActivate_ReturnCurrentSupplier()
    {
        using var factory = new SupplierApiFactory();
        using var client = factory.CreateClient();
        Authenticate(client);

        var createResponse = await client.PostAsJsonAsync("/api/suppliers", new
        {
            name = "Northstar Components",
            contactEmail = "orders@northstar.example"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<SupplierResult>();
        Assert.NotNull(created);
        Assert.True(created.IsActive);

        var updateResponse = await client.PutAsJsonAsync($"/api/suppliers/{created.Id}", new
        {
            name = "Northstar Components Europe",
            contactEmail = "europe@northstar.example"
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<SupplierResult>();
        Assert.NotNull(updated);
        Assert.Equal("Northstar Components Europe", updated.Name);
        Assert.Equal("europe@northstar.example", updated.ContactEmail);

        var deactivateResponse = await client.PostAsync($"/api/suppliers/{created.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        var deactivated = await deactivateResponse.Content.ReadFromJsonAsync<SupplierResult>();
        Assert.NotNull(deactivated);
        Assert.False(deactivated.IsActive);

        var activateResponse = await client.PostAsync($"/api/suppliers/{created.Id}/activate", null);

        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        var activated = await activateResponse.Content.ReadFromJsonAsync<SupplierResult>();
        Assert.NotNull(activated);
        Assert.True(activated.IsActive);
    }

    [Fact]
    public async Task SupplierManagement_ReturnsNotFoundForUnknownSupplier()
    {
        using var factory = new SupplierApiFactory();
        using var client = factory.CreateClient();
        Authenticate(client);

        var response = await client.PutAsJsonAsync($"/api/suppliers/{Guid.NewGuid()}", new
        {
            name = "Missing Supplier",
            contactEmail = "missing@example.test"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SupplierManagement_RejectsInvalidInput()
    {
        using var factory = new SupplierApiFactory();
        using var client = factory.CreateClient();
        Authenticate(client);

        var missingNameResponse = await client.PostAsJsonAsync("/api/suppliers", new
        {
            name = " ",
            contactEmail = "supplier@example.test"
        });
        var invalidEmailResponse = await client.PostAsJsonAsync("/api/suppliers", new
        {
            name = "Valid Supplier",
            contactEmail = "not-an-email"
        });
        var invalidIdResponse = await client.PutAsJsonAsync("/api/suppliers/not-a-guid", new
        {
            name = "Valid Supplier",
            contactEmail = "supplier@example.test"
        });

        Assert.Equal(HttpStatusCode.BadRequest, missingNameResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidEmailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidIdResponse.StatusCode);
    }

    [Fact]
    public async Task SupplierManagement_MutationsRequireAuthentication()
    {
        using var factory = new SupplierApiFactory();
        using var client = factory.CreateClient();
        var supplierId = Guid.NewGuid();

        var createResponse = await client.PostAsJsonAsync("/api/suppliers", new { name = "Northstar Components" });
        var updateResponse = await client.PutAsJsonAsync($"/api/suppliers/{supplierId}", new { name = "Northstar Components" });
        var activateResponse = await client.PostAsync($"/api/suppliers/{supplierId}/activate", null);
        var deactivateResponse = await client.PostAsync($"/api/suppliers/{supplierId}/deactivate", null);

        Assert.Equal(HttpStatusCode.Unauthorized, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, activateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deactivateResponse.StatusCode);
    }

    [Fact]
    public async Task SupplierManagement_PageEndpointReturnsFilteredEnvelopeAndValidatesPaging()
    {
        using var factory = new SupplierApiFactory();
        using var client = factory.CreateClient();
        Authenticate(client);

        await client.PostAsJsonAsync("/api/suppliers", new { name = "Northstar Components", contactEmail = "orders@northstar.example" });
        await client.PostAsJsonAsync("/api/suppliers", new { name = "Southwind Logistics", contactEmail = "dispatch@southwind.example" });

        var pageResponse = await client.GetAsync("/api/suppliers/page?pageIndex=0&pageSize=1&search=northstar");
        var page = await pageResponse.Content.ReadFromJsonAsync<PagedResult<SupplierResult>>();
        var invalidResponse = await client.GetAsync("/api/suppliers/page?pageIndex=-1&pageSize=25");
        var maximumPageResponse = await client.GetAsync("/api/suppliers/page?pageIndex=21474836&pageSize=100");
        var overflowingResponse = await client.GetAsync("/api/suppliers/page?pageIndex=21474837&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.NotNull(page);
        Assert.Equal(1, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal("Northstar Components", page.Items[0].Name);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, maximumPageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overflowingResponse.StatusCode);
    }

    private static void Authenticate(HttpClient client)
    {
        var now = DateTimeOffset.UtcNow;
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString("D")),
                new Claim("name", "test-user")
            ],
            notBefore: now.AddMinutes(-1).UtcDateTime,
            expires: now.AddMinutes(5).UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
    }

    private sealed class SupplierApiFactory : WebApplicationFactory<Program>
    {
        private readonly InMemorySupplierRepository supplierRepository = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
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
                services.AddSingleton<ISupplierRepository>(supplierRepository);
            });
        }
    }

    private sealed class InMemorySupplierRepository : ISupplierRepository
    {
        private readonly Dictionary<Guid, Supplier> suppliers = [];

        public Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Supplier>>([.. suppliers.Values.OrderBy(supplier => supplier.Name)]);

        public Task<PagedResult<Supplier>> PageAsync(SupplierPageRequest request, CancellationToken cancellationToken)
        {
            var matches = suppliers.Values
                .Where(supplier => string.IsNullOrWhiteSpace(request.Search)
                    || supplier.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase)
                    || supplier.ContactEmail?.Contains(request.Search, StringComparison.OrdinalIgnoreCase) == true)
                .OrderBy(supplier => supplier.Name)
                .ThenBy(supplier => supplier.Id)
                .ToArray();
            return Task.FromResult(new PagedResult<Supplier>(
                matches.Skip(request.Page.PageIndex * request.Page.PageSize).Take(request.Page.PageSize).ToArray(),
                matches.Length));
        }

        public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(suppliers.GetValueOrDefault(id));

        public Task<Supplier?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(suppliers.GetValueOrDefault(id));

        public Task CreateAsync(Supplier supplier, CancellationToken cancellationToken)
        {
            suppliers.Add(supplier.Id, supplier);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
