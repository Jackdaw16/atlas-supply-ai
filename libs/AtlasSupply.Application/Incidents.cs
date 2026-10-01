using AtlasSupply.Domain;

namespace AtlasSupply.Application;

public interface IIncidentRepository
{
    Task AddAsync(Incident incident, CancellationToken cancellationToken);

    Task<IReadOnlyList<Incident>> ListAsync(CancellationToken cancellationToken);

    Task<PagedResult<Incident>> PageAsync(IncidentPageRequest request, CancellationToken cancellationToken);

    Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Incident?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task UpdateAsync(Incident incident, CancellationToken cancellationToken);
}

public sealed record CreateIncidentInput(
    IncidentType Type,
    string Description,
    Guid SupplierId,
    Guid? PurchaseOrderId = null);

public sealed record CreateIncidentResult(
    Guid Id,
    IncidentType Type,
    IncidentStatus Status,
    string Description,
    Guid SupplierId,
    Guid? PurchaseOrderId,
    DateTime CreatedAtUtc);

public sealed record IncidentResult(
    Guid Id,
    string Type,
    string Status,
    Guid SupplierId,
    Guid? PurchaseOrderId,
    string Description,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? ClosedAtUtc);

public enum IncidentLifecycleFilter
{
    Open,
    Resolved
}

public sealed record IncidentPageRequest(
    PageRequest Page,
    string? Search,
    IncidentStatus? Status,
    Guid? SupplierId,
    IncidentType? Type,
    IncidentLifecycleFilter? Lifecycle);

public sealed class ListIncidents(IIncidentRepository incidentRepository)
{
    public async Task<IReadOnlyList<IncidentResult>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var incidents = await incidentRepository.ListAsync(cancellationToken);
        return incidents.Select(IncidentResults.From).ToArray();
    }
}

public sealed class PageIncidents(IIncidentRepository incidentRepository)
{
    public async Task<PagedResult<IncidentResult>> ExecuteAsync(
        IncidentPageRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Page.Validate();

        var result = await incidentRepository.PageAsync(request, cancellationToken);
        return new PagedResult<IncidentResult>(
            result.Items.Select(IncidentResults.From).ToArray(),
            result.TotalCount);
    }
}

public sealed record GetIncidentByIdInput(Guid IncidentId);

public sealed class GetIncidentById(IIncidentRepository incidentRepository)
{
    public async Task<IncidentResult?> ExecuteAsync(
        GetIncidentByIdInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        IncidentValidation.ValidateId(input.IncidentId);

        var incident = await incidentRepository.GetByIdAsync(input.IncidentId, cancellationToken);
        return incident is null ? null : IncidentResults.From(incident);
    }
}

public sealed class CreateIncident(
    ISupplierRepository supplierRepository,
    IPurchaseOrderRepository purchaseOrderRepository,
    IIncidentRepository incidentRepository)
{
    public async Task<CreateIncidentResult> ExecuteAsync(
        CreateIncidentInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!Enum.IsDefined(input.Type))
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.Type, "Incident type is invalid.");
        }

        if (string.IsNullOrWhiteSpace(input.Description))
        {
            throw new ArgumentException("Incident description is required.", nameof(input));
        }

        var description = input.Description.Trim();
        if (description.Length > 2000)
        {
            throw new ArgumentException("Incident description cannot exceed 2000 characters.", nameof(input));
        }

        if (input.SupplierId == Guid.Empty)
        {
            throw new ArgumentException("Supplier id is required.", nameof(input));
        }

        var supplier = await supplierRepository.GetByIdAsync(input.SupplierId, cancellationToken);
        if (supplier is null)
        {
            throw new InvalidOperationException("Supplier was not found.");
        }

        if (input.PurchaseOrderId is Guid purchaseOrderId)
        {
            if (purchaseOrderId == Guid.Empty)
            {
                throw new ArgumentException("Purchase order id cannot be empty.", nameof(input));
            }

            var purchaseOrder = await purchaseOrderRepository.GetByIdAsync(purchaseOrderId, cancellationToken);
            if (purchaseOrder is null)
            {
                throw new InvalidOperationException("Purchase order was not found.");
            }

            if (purchaseOrder.SupplierId != input.SupplierId)
            {
                throw new InvalidOperationException("Purchase order does not belong to the supplier.");
            }
        }

        var incident = new Incident(
            Guid.NewGuid(),
            input.Type,
            description,
            input.SupplierId,
            input.PurchaseOrderId);

        await incidentRepository.AddAsync(incident, cancellationToken);

        return new CreateIncidentResult(
            incident.Id,
            incident.Type,
            incident.Status,
            incident.Description,
            incident.SupplierId,
            incident.PurchaseOrderId,
            incident.CreatedAtUtc);
    }
}

public sealed record UpdateIncidentDescriptionInput(Guid IncidentId, string Description);

public sealed class UpdateIncidentDescription(IIncidentRepository incidentRepository)
{
    public async Task<IncidentResult?> ExecuteAsync(
        UpdateIncidentDescriptionInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        IncidentValidation.ValidateId(input.IncidentId);
        var description = IncidentValidation.ValidateDescription(input.Description);

        var incident = await incidentRepository.GetForUpdateAsync(input.IncidentId, cancellationToken);
        if (incident is null)
        {
            return null;
        }

        incident.SetDescription(description);
        await incidentRepository.UpdateAsync(incident, cancellationToken);
        return IncidentResults.From(incident);
    }
}

public sealed record ResolveIncidentInput(Guid IncidentId);

public sealed class ResolveIncident(IIncidentRepository incidentRepository)
{
    public async Task<IncidentResult?> ExecuteAsync(
        ResolveIncidentInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        IncidentValidation.ValidateId(input.IncidentId);

        var incident = await incidentRepository.GetForUpdateAsync(input.IncidentId, cancellationToken);
        if (incident is null)
        {
            return null;
        }

        incident.Resolve();
        await incidentRepository.UpdateAsync(incident, cancellationToken);
        return IncidentResults.From(incident);
    }
}

internal static class IncidentResults
{
    internal static IncidentResult From(Incident incident) => new(
        incident.Id,
        incident.Type.ToString(),
        incident.Status.ToString(),
        incident.SupplierId,
        incident.PurchaseOrderId,
        incident.Description,
        incident.CreatedAtUtc,
        incident.ResolvedAtUtc,
        incident.ClosedAtUtc);
}

internal static class IncidentValidation
{
    internal static void ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Incident id is required.", nameof(id));
        }
    }

    internal static string ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Incident description is required.", nameof(description));
        }

        var trimmedDescription = description.Trim();
        if (trimmedDescription.Length > 2000)
        {
            throw new ArgumentException("Incident description cannot exceed 2000 characters.", nameof(description));
        }

        return trimmedDescription;
    }
}
