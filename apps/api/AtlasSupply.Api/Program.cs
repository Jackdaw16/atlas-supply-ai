using AtlasSupply.Application;
using AtlasSupply.Api;
using AtlasSupply.Infrastructure;
using AtlasSupply.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(
            "https://atlas-supply.jalejandrodc.com",
            "http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod());
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = JwtConfiguration.ResolveValidationSettings(builder.Configuration);

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "name"
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ListSuppliers>();
builder.Services.AddScoped<GetSupplierById>();
builder.Services.AddScoped<GetDelayedOrders>();
builder.Services.AddScoped<CreateIncident>();
builder.Services.AddScoped<Login>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapAtlasSupplyEndpoints();

app.Run();

public partial class Program;
