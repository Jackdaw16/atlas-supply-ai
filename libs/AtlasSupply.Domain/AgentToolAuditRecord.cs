namespace AtlasSupply.Domain;

public sealed class AgentToolAuditRecord
{
    private AgentToolAuditRecord()
    {
    }

    public AgentToolAuditRecord(
        Guid userId,
        string username,
        string toolName,
        string requiredScope,
        bool authorized,
        bool succeeded,
        DateTime timestampUtc,
        string outcome)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        Username = RequireValue(username, "Username", 100);
        ToolName = RequireValue(toolName, "Tool name", 100);
        RequiredScope = RequireValue(requiredScope, "Required scope", 64);
        Authorized = authorized;
        Succeeded = succeeded;
        TimestampUtc = timestampUtc.Kind == DateTimeKind.Utc
            ? timestampUtc
            : throw new ArgumentException("Timestamp must be UTC.", nameof(timestampUtc));
        Outcome = RequireValue(outcome, "Outcome", 64);
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public string ToolName { get; private set; } = string.Empty;

    public string RequiredScope { get; private set; } = string.Empty;

    public bool Authorized { get; private set; }

    public bool Succeeded { get; private set; }

    public DateTime TimestampUtc { get; private set; }

    public string Outcome { get; private set; } = string.Empty;

    public void Complete(bool succeeded, string outcome)
    {
        Succeeded = succeeded;
        Outcome = RequireValue(outcome, "Outcome", 64);
    }

    private static string RequireValue(string value, string valueName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalizedValue = value.Trim();

        if (normalizedValue.Length > maximumLength)
        {
            throw new ArgumentException($"{valueName} exceeds the maximum length of {maximumLength}.", valueName);
        }

        return normalizedValue;
    }
}
