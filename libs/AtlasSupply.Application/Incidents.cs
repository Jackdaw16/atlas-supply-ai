using AtlasSupply.Domain;

namespace AtlasSupply.Application;

public interface IIncidentRepository
{
    Task AddAsync(Incident incident, CancellationToken cancellationToken);
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
