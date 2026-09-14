namespace AtlasSupply.Domain;

public sealed class PurchaseOrderItem
{
    public PurchaseOrderItem(Guid id, string description, int quantity, decimal unitPrice)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Purchase order item id is required.", nameof(id));
        }

        Id = id;
        SetDescription(description);
        SetQuantity(quantity);
        SetUnitPrice(unitPrice);
    }

    public Guid Id { get; }

    public string Description { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;

    public void SetDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Item description is required.", nameof(description));
        }

        Description = description.Trim();
    }

    public void SetQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Item quantity must be greater than zero.");
        }

        Quantity = quantity;
    }

    public void SetUnitPrice(decimal unitPrice)
    {
        if (unitPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Item unit price must be greater than zero.");
        }

        UnitPrice = unitPrice;
    }
}
