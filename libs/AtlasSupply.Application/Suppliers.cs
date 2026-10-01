using AtlasSupply.Domain;

namespace AtlasSupply.Application;

public interface ISupplierRepository
{
    Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken);

    Task<PagedResult<Supplier>> PageAsync(SupplierPageRequest request, CancellationToken cancellationToken);

    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Supplier?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task CreateAsync(Supplier supplier, CancellationToken cancellationToken);

    Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken);
}

public sealed record SupplierResult(
    Guid Id,
    string Name,
    string? ContactEmail,
    bool IsActive);

public sealed record SupplierPageRequest(PageRequest Page, string? Search);

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

        return supplier is null ? null : SupplierResults.From(supplier);
    }
}

public sealed class PageSuppliers(ISupplierRepository supplierRepository)
{
    public async Task<PagedResult<SupplierResult>> ExecuteAsync(
        SupplierPageRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Page.Validate();

        var result = await supplierRepository.PageAsync(request, cancellationToken);
        return new PagedResult<SupplierResult>(
            result.Items.Select(SupplierResults.From).ToArray(),
            result.TotalCount);
    }
}

public sealed record CreateSupplierInput(string Name, string? ContactEmail);

public sealed class CreateSupplier(ISupplierRepository supplierRepository)
{
    public async Task<SupplierResult> ExecuteAsync(
        CreateSupplierInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        SupplierValidation.ValidateDetails(input.Name, input.ContactEmail);

        var supplier = new Supplier(Guid.NewGuid(), input.Name, input.ContactEmail);
        await supplierRepository.CreateAsync(supplier, cancellationToken);

        return SupplierResults.From(supplier);
    }
}

public sealed record UpdateSupplierInput(Guid SupplierId, string Name, string? ContactEmail);

public sealed class UpdateSupplier(ISupplierRepository supplierRepository)
{
    public async Task<SupplierResult?> ExecuteAsync(
        UpdateSupplierInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        SupplierValidation.ValidateId(input.SupplierId);
        SupplierValidation.ValidateDetails(input.Name, input.ContactEmail);

        var supplier = await supplierRepository.GetForUpdateAsync(input.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return null;
        }

        supplier.SetName(input.Name);
        supplier.SetContactEmail(input.ContactEmail);
        await supplierRepository.UpdateAsync(supplier, cancellationToken);

        return SupplierResults.From(supplier);
    }
}

public sealed record ActivateSupplierInput(Guid SupplierId);

public sealed class ActivateSupplier(ISupplierRepository supplierRepository)
{
    public async Task<SupplierResult?> ExecuteAsync(
        ActivateSupplierInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        SupplierValidation.ValidateId(input.SupplierId);

        var supplier = await supplierRepository.GetForUpdateAsync(input.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return null;
        }

        supplier.Activate();
        await supplierRepository.UpdateAsync(supplier, cancellationToken);

        return SupplierResults.From(supplier);
    }
}

public sealed record DeactivateSupplierInput(Guid SupplierId);

public sealed class DeactivateSupplier(ISupplierRepository supplierRepository)
{
    public async Task<SupplierResult?> ExecuteAsync(
        DeactivateSupplierInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        SupplierValidation.ValidateId(input.SupplierId);

        var supplier = await supplierRepository.GetForUpdateAsync(input.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return null;
        }

        supplier.Deactivate();
        await supplierRepository.UpdateAsync(supplier, cancellationToken);

        return SupplierResults.From(supplier);
    }
}

internal static class SupplierResults
{
    internal static SupplierResult From(Supplier supplier) => new(
        supplier.Id,
        supplier.Name,
        supplier.ContactEmail,
        supplier.IsActive);
}

internal static class SupplierValidation
{
    internal static void ValidateId(Guid supplierId)
    {
        if (supplierId == Guid.Empty)
        {
            throw new ArgumentException("Supplier id is required.", nameof(supplierId));
        }
    }

    internal static void ValidateDetails(string name, string? contactEmail)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Supplier name is required.", nameof(name));
        }

        if (name.Trim().Length > 200)
        {
            throw new ArgumentException("Supplier name cannot exceed 200 characters.", nameof(name));
        }

        if (contactEmail?.Trim().Length > 320)
        {
            throw new ArgumentException("Contact email cannot exceed 320 characters.", nameof(contactEmail));
        }
    }
}
