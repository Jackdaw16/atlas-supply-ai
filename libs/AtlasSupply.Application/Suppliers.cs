using AtlasSupply.Domain;

namespace AtlasSupply.Application;

public interface ISupplierRepository
{
    Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken);

    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record SupplierResult(
    Guid Id,
    string Name,
    string? ContactEmail,
    bool IsActive);

public sealed class ListSuppliers(ISupplierRepository supplierRepository)
{
    public async Task<IReadOnlyList<SupplierResult>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var suppliers = await supplierRepository.ListAsync(cancellationToken);

        return suppliers
            .Select(static supplier => new SupplierResult(
                supplier.Id,
                supplier.Name,
                supplier.ContactEmail,
                supplier.IsActive))
            .ToArray();
    }
}

public sealed record GetSupplierByIdInput(Guid SupplierId);

public sealed class GetSupplierById(ISupplierRepository supplierRepository)
{
    public async Task<SupplierResult?> ExecuteAsync(
        GetSupplierByIdInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.SupplierId == Guid.Empty)
        {
            throw new ArgumentException("Supplier id is required.", nameof(input));
        }

        var supplier = await supplierRepository.GetByIdAsync(input.SupplierId, cancellationToken);

        return supplier is null
            ? null
            : new SupplierResult(
                supplier.Id,
                supplier.Name,
                supplier.ContactEmail,
                supplier.IsActive);
    }
}
