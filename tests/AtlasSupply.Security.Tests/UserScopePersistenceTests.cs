using AtlasSupply.Domain;
using AtlasSupply.Infrastructure.Persistence;
using AtlasSupply.Infrastructure.Persistence.Seed;
using AtlasSupply.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace AtlasSupply.Security.Tests;

public sealed class UserScopePersistenceTests
{
    [Theory]
    [InlineData("55555555-5555-5555-5555-555555555501", "readonly", "readonly@atlas-supply.local")]
    [InlineData("55555555-5555-5555-5555-555555555502", "operator", "operator@atlas-supply.local")]
    public void SeededDemoUserPasswordHash_ValidatesTheDevelopmentPassword(string userId, string username, string email)
    {
        var user = new User(Guid.Parse(userId), username, email, AtlasSupplySeedData.DemoUserPasswordHash);
        var passwordHasher = new UserPasswordHasher();

        Assert.Equal(
            PasswordVerificationResult.Success,
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, "test-only-password"));
    }

    [Fact]
    public void UserScopeAssignments_AreMappedWithTheUserAndPasswordHash()
    {
        var options = CreateOptions();
        var user = new User(
            Guid.Parse("77777777-7777-7777-7777-777777777701"),
            "warehouse.operator",
            "warehouse.operator@atlas-supply.local",
            "placeholder");
        var passwordHasher = new UserPasswordHasher();
        user.SetPasswordHash(passwordHasher.HashPassword(user, "test-only-password"));
        user.AssignScope(AgentCapabilityScope.SuppliersList);
        user.AssignScope(AgentCapabilityScope.IncidentsCreate);

        using var context = new AtlasSupplyDbContext(options);
        var userEntityType = context.Model.FindEntityType(typeof(User))!;
        var assignmentEntityType = context.Model.FindEntityType(typeof(UserScopeAssignment))!;
        var foreignKey = Assert.Single(assignmentEntityType.GetForeignKeys());

        Assert.Equal("users", userEntityType.GetTableName());
        Assert.Equal("user_scope_assignments", assignmentEntityType.GetTableName());
        Assert.Same(userEntityType, foreignKey.PrincipalEntityType);
        Assert.True(foreignKey.IsRequired);
        Assert.Equal([nameof(UserScopeAssignment.UserId), nameof(UserScopeAssignment.Scope)], assignmentEntityType.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.NotNull(assignmentEntityType.FindProperty(nameof(UserScopeAssignment.Scope))!.GetValueConverter());
        Assert.Equal("WAREHOUSE.OPERATOR", user.NormalizedUsername);
        Assert.Equal("WAREHOUSE.OPERATOR@ATLAS-SUPPLY.LOCAL", user.NormalizedEmail);
        Assert.Equal(
            PasswordVerificationResult.Success,
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, "test-only-password"));
        Assert.Equal(
            [AgentCapabilityScope.IncidentsCreate, AgentCapabilityScope.SuppliersList],
            user.ScopeAssignments.Select(assignment => assignment.Scope).OrderBy(scope => scope.Value));
    }

    [Fact]
    public void Model_RequiresUniqueNormalizedUsernamesAndEmails()
    {
        using var context = new AtlasSupplyDbContext(CreateOptions());
        var userEntityType = context.Model.FindEntityType(typeof(User))!;

        Assert.Contains(
            userEntityType.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(User.NormalizedUsername)]));
        Assert.Contains(
            userEntityType.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(User.NormalizedEmail)]));
    }

    [Fact]
    public void AgentCapabilityScopes_OnlyAcceptDefinedValues()
    {
        Assert.Equal(AgentCapabilityScope.KnowledgeSearch, AgentCapabilityScope.FromValue("knowledge.search"));
        Assert.Throws<ArgumentException>(() => AgentCapabilityScope.FromValue("orders.write"));
    }

    [Fact]
    public void AgentToolAuditRecords_AreMappedWithRequiredFieldsIndexesAndNoArguments()
    {
        using var context = new AtlasSupplyDbContext(CreateOptions());
        var auditEntityType = context.Model.FindEntityType(typeof(AgentToolAuditRecord))!;

        Assert.Equal("agent_tool_audit_records", auditEntityType.GetTableName());
        Assert.All(
            [
                nameof(AgentToolAuditRecord.UserId),
                nameof(AgentToolAuditRecord.Username),
                nameof(AgentToolAuditRecord.ToolName),
                nameof(AgentToolAuditRecord.RequiredScope),
                nameof(AgentToolAuditRecord.Authorized),
                nameof(AgentToolAuditRecord.Succeeded),
                nameof(AgentToolAuditRecord.TimestampUtc),
                nameof(AgentToolAuditRecord.Outcome)
            ],
            propertyName => Assert.False(auditEntityType.FindProperty(propertyName)!.IsNullable));
        Assert.Contains(
            auditEntityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(AgentToolAuditRecord.TimestampUtc)]));
        Assert.Contains(
            auditEntityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(AgentToolAuditRecord.UserId)]));
        Assert.Contains(
            auditEntityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(AgentToolAuditRecord.ToolName)]));
        Assert.Contains(
            auditEntityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(AgentToolAuditRecord.Authorized)]));
        Assert.Null(auditEntityType.FindProperty("Arguments"));
        Assert.Null(auditEntityType.FindProperty("AccessToken"));
        Assert.Null(auditEntityType.FindProperty("AuthorizationHeader"));
    }

    private static DbContextOptions<AtlasSupplyDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AtlasSupplyDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=atlas_supply_security_test;Username=atlas_supply;Password=not-used",
                npgsqlOptions => npgsqlOptions.UseVector())
            .Options;
    }
}
