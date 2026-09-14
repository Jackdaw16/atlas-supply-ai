namespace AtlasSupply.Domain;

public sealed class PurchaseOrder
{
    private readonly List<PurchaseOrderItem> _items = [];

    public PurchaseOrder(Guid id, Guid supplierId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Purchase order id is required.", nameof(id));
        }

        if (supplierId == Guid.Empty)
        {
            throw new ArgumentException("Supplier id is required.", nameof(supplierId));
        }

        Id = id;
        SupplierId = supplierId;
        Status = PurchaseOrderStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; }

    public Guid SupplierId { get; }

    public PurchaseOrderStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public DateTime? SubmittedAtUtc { get; private set; }

    public DateTime? ApprovedAtUtc { get; private set; }

    public DateTime? ReceivedAtUtc { get; private set; }

    public IReadOnlyCollection<PurchaseOrderItem> Items => _items.AsReadOnly();

    public decimal TotalAmount => _items.Sum(static x => x.LineTotal);

    public void AddItem(PurchaseOrderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        EnsureEditable();

        if (_items.Any(x => x.Id == item.Id))
        {
            throw new InvalidOperationException("Duplicate purchase order item id.");
        }

        _items.Add(item);
    }

    public void RemoveItem(Guid itemId)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("Item id is required.", nameof(itemId));
        }

        EnsureEditable();

        var removed = _items.RemoveAll(x => x.Id == itemId);
        if (removed == 0)
        {
            throw new InvalidOperationException("Item was not found in purchase order.");
        }
    }

    public void Submit()
    {
        if (Status != PurchaseOrderStatus.Draft)
        {
            throw new InvalidOperationException("Only draft purchase orders can be submitted.");
        }

        if (_items.Count == 0)
        {
            throw new InvalidOperationException("Purchase order requires at least one item.");
        }

        Status = PurchaseOrderStatus.Submitted;
        SubmittedAtUtc = DateTime.UtcNow;
    }

    public void Approve()
    {
        if (Status != PurchaseOrderStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted purchase orders can be approved.");
        }

        Status = PurchaseOrderStatus.Approved;
        ApprovedAtUtc = DateTime.UtcNow;
    }

    public void MarkReceived()
    {
        if (Status != PurchaseOrderStatus.Approved)
        {
            throw new InvalidOperationException("Only approved purchase orders can be marked as received.");
        }

        Status = PurchaseOrderStatus.Received;
        ReceivedAtUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == PurchaseOrderStatus.Received)
        {
            throw new InvalidOperationException("Received purchase orders cannot be cancelled.");
        }

        if (Status == PurchaseOrderStatus.Cancelled)
        {
            return;
        }

        Status = PurchaseOrderStatus.Cancelled;
    }

    private void EnsureEditable()
    {
        if (Status != PurchaseOrderStatus.Draft)
        {
            throw new InvalidOperationException("Purchase order items can be changed only while draft.");
        }
    }
}
