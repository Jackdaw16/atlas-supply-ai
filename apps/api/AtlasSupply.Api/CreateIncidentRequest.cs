using AtlasSupply.Domain;
using System.Text.Json.Serialization;

namespace AtlasSupply.Api;

public sealed record CreateIncidentRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter<IncidentType>))]
    IncidentType? Type,
    string? Description,
    Guid? SupplierId,
    Guid? PurchaseOrderId);

public sealed record UpdateIncidentDescriptionRequest(string? Description);
