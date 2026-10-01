using System.Net.Mail;

namespace AtlasSupply.Domain;

public sealed class Supplier
{
    public Supplier(Guid id, string name, string? contactEmail = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Supplier id is required.", nameof(id));
        }

        Id = id;
        SetName(name);
        SetContactEmail(contactEmail);
        IsActive = true;
    }

    public Guid Id { get; }

    public string Name { get; private set; } = string.Empty;

    public string? ContactEmail { get; private set; }

    public bool IsActive { get; private set; }

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Supplier name is required.", nameof(name));
        }

        Name = name.Trim();
    }

    public void SetContactEmail(string? contactEmail)
    {
        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            ContactEmail = null;
            return;
        }

        var normalized = contactEmail.Trim();

        try
        {
            var emailAddress = new MailAddress(normalized);
            if (!string.Equals(emailAddress.Address, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Contact email is invalid.", nameof(contactEmail));
            }
        }
        catch (FormatException)
        {
            throw new ArgumentException("Contact email is invalid.", nameof(contactEmail));
        }

        ContactEmail = normalized;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
