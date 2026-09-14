namespace AtlasSupply.Domain;

public sealed class Incident
{
    public Incident(
        Guid id,
        IncidentType type,
        string description,
        Guid supplierId,
        Guid? purchaseOrderId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Incident id is required.", nameof(id));
        }

        if (supplierId == Guid.Empty)
        {
            throw new ArgumentException("Supplier id is required.", nameof(supplierId));
        }

        if (purchaseOrderId is Guid poId && poId == Guid.Empty)
        {
            throw new ArgumentException("Purchase order id cannot be empty.", nameof(purchaseOrderId));
        }

        Id = id;
        Type = type;
        SupplierId = supplierId;
        PurchaseOrderId = purchaseOrderId;
        CreatedAtUtc = DateTime.UtcNow;
        Status = IncidentStatus.Open;
        SetDescription(description);
    }

    public Guid Id { get; }

    public IncidentType Type { get; }

    public IncidentStatus Status { get; private set; }

    public Guid SupplierId { get; }

    public Guid? PurchaseOrderId { get; }

    public string Description { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public DateTime? ClosedAtUtc { get; private set; }

    public void SetDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Incident description is required.", nameof(description));
        }

        Description = description.Trim();
    }

    public void StartProgress()
    {
        if (Status != IncidentStatus.Open)
        {
            throw new InvalidOperationException("Only open incidents can move to in-progress.");
        }

        Status = IncidentStatus.InProgress;
    }

    public void Resolve()
    {
        if (Status is not IncidentStatus.Open and not IncidentStatus.InProgress)
        {
            throw new InvalidOperationException("Only open or in-progress incidents can be resolved.");
        }

        Status = IncidentStatus.Resolved;
        ResolvedAtUtc = DateTime.UtcNow;
    }

    public void Close()
    {
        if (Status != IncidentStatus.Resolved)
        {
            throw new InvalidOperationException("Only resolved incidents can be closed.");
        }

        Status = IncidentStatus.Closed;
        ClosedAtUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status is IncidentStatus.Closed or IncidentStatus.Resolved)
        {
            throw new InvalidOperationException("Resolved or closed incidents cannot be cancelled.");
        }

        Status = IncidentStatus.Cancelled;
    }
}
