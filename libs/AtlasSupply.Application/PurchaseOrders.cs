using AtlasSupply.Domain;

namespace AtlasSupply.Application;

public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PurchaseOrder?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PurchaseOrder>> ListOutstandingApprovedAsync(CancellationToken cancellationToken);

    Task CreateAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken);

    Task UpdateAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken);
}

public sealed record DelayedOrderResult(
    Guid Id,
    Guid SupplierId,
    DateTime ApprovedAtUtc);

public sealed class GetDelayedOrders(IPurchaseOrderRepository purchaseOrderRepository)
{
    public async Task<IReadOnlyList<DelayedOrderResult>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var purchaseOrders = await purchaseOrderRepository.ListOutstandingApprovedAsync(cancellationToken);

        return purchaseOrders
            .Select(static purchaseOrder => new DelayedOrderResult(
                purchaseOrder.Id,
                purchaseOrder.SupplierId,
                purchaseOrder.ApprovedAtUtc!.Value))
            .ToArray();
    }
}

public sealed record PurchaseOrderItemInput(string Description, int Quantity, decimal UnitPrice);

public sealed record PurchaseOrderItemResult(
    Guid Id,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record PurchaseOrderResult(
    Guid Id,
    Guid SupplierId,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? ApprovedAtUtc,
    DateTime? ReceivedAtUtc,
    IReadOnlyList<PurchaseOrderItemResult> Items,
    decimal TotalAmount,
    bool IsDelayed);

public sealed class ListPurchaseOrders(IPurchaseOrderRepository purchaseOrderRepository)
{
    public async Task<IReadOnlyList<PurchaseOrderResult>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var purchaseOrders = await purchaseOrderRepository.ListAsync(cancellationToken);
        return purchaseOrders.Select(PurchaseOrderResults.From).ToArray();
    }
}

public sealed record GetPurchaseOrderByIdInput(Guid PurchaseOrderId);

public sealed class GetPurchaseOrderById(IPurchaseOrderRepository purchaseOrderRepository)
{
    public async Task<PurchaseOrderResult?> ExecuteAsync(
        GetPurchaseOrderByIdInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        PurchaseOrderValidation.ValidateId(input.PurchaseOrderId);

        var purchaseOrder = await purchaseOrderRepository.GetByIdAsync(input.PurchaseOrderId, cancellationToken);
        return purchaseOrder is null ? null : PurchaseOrderResults.From(purchaseOrder);
    }
}

public sealed record CreatePurchaseOrderInput(Guid SupplierId, IReadOnlyList<PurchaseOrderItemInput> Items);

public sealed class CreatePurchaseOrder(
    IPurchaseOrderRepository purchaseOrderRepository,
    ISupplierRepository supplierRepository)
{
    public async Task<PurchaseOrderResult?> ExecuteAsync(
        CreatePurchaseOrderInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        PurchaseOrderValidation.ValidateId(input.SupplierId, "Supplier id is required.");
        PurchaseOrderValidation.ValidateItems(input.Items, requireAtLeastOne: true);

        var supplier = await supplierRepository.GetByIdAsync(input.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return null;
        }

        var purchaseOrder = new PurchaseOrder(Guid.NewGuid(), input.SupplierId);
        foreach (var item in input.Items)
        {
            purchaseOrder.AddItem(PurchaseOrderResults.ToDomainItem(item));
        }

        await purchaseOrderRepository.CreateAsync(purchaseOrder, cancellationToken);
        return PurchaseOrderResults.From(purchaseOrder);
    }
}

public sealed record UpdatePurchaseOrderDraftItemsInput(
    Guid PurchaseOrderId,
    IReadOnlyList<PurchaseOrderItemInput> Items);

public sealed class UpdatePurchaseOrderDraftItems(IPurchaseOrderRepository purchaseOrderRepository)
{
    public async Task<PurchaseOrderResult?> ExecuteAsync(
        UpdatePurchaseOrderDraftItemsInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        PurchaseOrderValidation.ValidateId(input.PurchaseOrderId);
        PurchaseOrderValidation.ValidateItems(input.Items, requireAtLeastOne: true);

        var purchaseOrder = await purchaseOrderRepository.GetForUpdateAsync(input.PurchaseOrderId, cancellationToken);
        if (purchaseOrder is null)
        {
            return null;
        }

        foreach (var item in purchaseOrder.Items.ToArray())
        {
            purchaseOrder.RemoveItem(item.Id);
        }

        foreach (var item in input.Items)
        {
            purchaseOrder.AddItem(PurchaseOrderResults.ToDomainItem(item));
        }

        await purchaseOrderRepository.UpdateAsync(purchaseOrder, cancellationToken);
        return PurchaseOrderResults.From(purchaseOrder);
    }
}

public sealed record SubmitPurchaseOrderInput(Guid PurchaseOrderId);

public sealed class SubmitPurchaseOrder(IPurchaseOrderRepository purchaseOrderRepository)
{
    public Task<PurchaseOrderResult?> ExecuteAsync(SubmitPurchaseOrderInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return PurchaseOrderTransition.ExecuteAsync(input.PurchaseOrderId, purchaseOrderRepository, static order => order.Submit(), cancellationToken);
    }
}

public sealed record ApprovePurchaseOrderInput(Guid PurchaseOrderId);

public sealed class ApprovePurchaseOrder(IPurchaseOrderRepository purchaseOrderRepository)
{
    public Task<PurchaseOrderResult?> ExecuteAsync(ApprovePurchaseOrderInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return PurchaseOrderTransition.ExecuteAsync(input.PurchaseOrderId, purchaseOrderRepository, static order => order.Approve(), cancellationToken);
    }
}

public sealed record MarkPurchaseOrderReceivedInput(Guid PurchaseOrderId);

public sealed class MarkPurchaseOrderReceived(IPurchaseOrderRepository purchaseOrderRepository)
{
    public Task<PurchaseOrderResult?> ExecuteAsync(MarkPurchaseOrderReceivedInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return PurchaseOrderTransition.ExecuteAsync(input.PurchaseOrderId, purchaseOrderRepository, static order => order.MarkReceived(), cancellationToken);
    }
}

public sealed record CancelPurchaseOrderInput(Guid PurchaseOrderId);

public sealed class CancelPurchaseOrder(IPurchaseOrderRepository purchaseOrderRepository)
{
    public Task<PurchaseOrderResult?> ExecuteAsync(CancelPurchaseOrderInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return PurchaseOrderTransition.ExecuteAsync(input.PurchaseOrderId, purchaseOrderRepository, static order => order.Cancel(), cancellationToken);
    }
}

internal static class PurchaseOrderTransition
{
    internal static async Task<PurchaseOrderResult?> ExecuteAsync(
        Guid purchaseOrderId,
        IPurchaseOrderRepository purchaseOrderRepository,
        Action<PurchaseOrder> transition,
        CancellationToken cancellationToken)
    {
        PurchaseOrderValidation.ValidateId(purchaseOrderId);
        var purchaseOrder = await purchaseOrderRepository.GetForUpdateAsync(purchaseOrderId, cancellationToken);
        if (purchaseOrder is null)
        {
            return null;
        }

        transition(purchaseOrder);
        await purchaseOrderRepository.UpdateAsync(purchaseOrder, cancellationToken);
        return PurchaseOrderResults.From(purchaseOrder);
    }
}

internal static class PurchaseOrderResults
{
    internal static PurchaseOrderResult From(PurchaseOrder purchaseOrder) => new(
        purchaseOrder.Id,
        purchaseOrder.SupplierId,
        purchaseOrder.Status.ToString(),
        purchaseOrder.CreatedAtUtc,
        purchaseOrder.SubmittedAtUtc,
        purchaseOrder.ApprovedAtUtc,
        purchaseOrder.ReceivedAtUtc,
        purchaseOrder.Items.Select(static item => new PurchaseOrderItemResult(
            item.Id,
            item.Description,
            item.Quantity,
            item.UnitPrice,
            item.LineTotal)).ToArray(),
        purchaseOrder.TotalAmount,
        purchaseOrder.Status == PurchaseOrderStatus.Approved
            && purchaseOrder.ApprovedAtUtc is not null
            && purchaseOrder.ReceivedAtUtc is null);

    internal static PurchaseOrderItem ToDomainItem(PurchaseOrderItemInput input) =>
        new(Guid.NewGuid(), input.Description, input.Quantity, input.UnitPrice);
}

public static class PurchaseOrderValidation
{
    internal static void ValidateId(Guid id, string message = "Purchase order id is required.")
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(message, nameof(id));
        }
    }

    internal static void ValidateItems(IReadOnlyList<PurchaseOrderItemInput>? items, bool requireAtLeastOne)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (requireAtLeastOne && items.Count == 0)
        {
            throw new ArgumentException("Purchase order requires at least one item.", nameof(items));
        }

        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            _ = PurchaseOrderResults.ToDomainItem(item);
        }

        if (!HasSafeTotal(items))
        {
            throw new ArgumentOutOfRangeException(nameof(items), "Purchase order total exceeds the supported monetary range.");
        }
    }

    public static bool HasSafeTotal(IReadOnlyList<PurchaseOrderItemInput> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        try
        {
            decimal total = 0;
            foreach (var item in items)
            {
                ArgumentNullException.ThrowIfNull(item);
                total = checked(total + checked(item.Quantity * item.UnitPrice));
            }

            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
