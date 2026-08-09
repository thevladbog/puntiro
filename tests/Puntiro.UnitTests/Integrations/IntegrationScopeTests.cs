using System.Diagnostics;
using Puntiro.Modules.Integrations.Domain;
using Xunit;

namespace Puntiro.UnitTests.Integrations;

public sealed class IntegrationScopeTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Token_normalizes_display_name_and_deduplicates_approved_scopes()
    {
        var token = Create(
            "  Warehouse ERP  ",
            new HashSet<IntegrationScope>
            {
                IntegrationScope.ShipmentsWrite,
                IntegrationScope.ShipmentsRead
            });

        Assert.Equal("Warehouse ERP", token.DisplayName);
        Assert.Equal(
            new[] { IntegrationScope.ShipmentsRead, IntegrationScope.ShipmentsWrite },
            token.ScopeValues.OrderBy(static scope => scope));
    }

    [Fact]
    public void Token_rejects_empty_and_unknown_scope_sets()
    {
        Assert.Throws<ArgumentException>(() =>
            Create("ERP", new HashSet<IntegrationScope>()));
        Assert.Throws<ArgumentException>(() =>
            Create("ERP", new HashSet<IntegrationScope> { (IntegrationScope)999 }));
    }

    [Fact]
    public void Display_name_uses_unicode_scalar_count_and_rejects_invalid_utf16()
    {
        var hundredEmoji = string.Concat(Enumerable.Repeat("🟠", 100));
        var hundredOneEmoji = hundredEmoji + "🟠";

        Assert.Equal(hundredEmoji, Create(hundredEmoji, ApprovedScopes()).DisplayName);
        Assert.Throws<ArgumentException>(() => Create(hundredOneEmoji, ApprovedScopes()));
        Assert.Throws<ArgumentException>(() => Create("\ud800", ApprovedScopes()));
        Assert.Throws<ArgumentException>(() => Create("   ", ApprovedScopes()));
    }

    [Fact]
    public void Security_event_uses_the_bounded_w3c_activity_trace_when_available()
    {
        using var activity = new Activity("integration-token-create")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        var securityEvent = IntegrationSecurityEvent.Created(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Now);

        Assert.Equal(activity.TraceId.ToString(), securityEvent.TraceId);
        Assert.Matches("^[0-9a-f]{32}$", securityEvent.TraceId);
        Assert.InRange(securityEvent.TraceId.Length, 1, IntegrationSecurityEvent.MaximumTraceIdLength);
    }

    [Fact]
    public void Security_event_uses_a_safe_fallback_for_a_non_w3c_activity()
    {
        using var activity = new Activity("integration-token-create")
            .SetIdFormat(ActivityIdFormat.Hierarchical)
            .Start();

        var securityEvent = IntegrationSecurityEvent.Created(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Now);

        Assert.StartsWith("trace:integration:", securityEvent.TraceId, StringComparison.Ordinal);
        Assert.InRange(securityEvent.TraceId.Length, 1, IntegrationSecurityEvent.MaximumTraceIdLength);
    }

    private static IntegrationToken Create(
        string displayName,
        IReadOnlySet<IntegrationScope> scopes) =>
        IntegrationToken.Issue(
            Guid.CreateVersion7(),
            "AAECAwQFBgcICQoLDA0ODw",
            Guid.CreateVersion7(),
            displayName,
            Enumerable.Repeat((byte)0x51, 32).ToArray(),
            "integration-v1",
            Guid.CreateVersion7(),
            Now,
            scopes,
            activeSlot: 1);

    private static IReadOnlySet<IntegrationScope> ApprovedScopes() =>
        new HashSet<IntegrationScope> { IntegrationScope.ShipmentsRead };
}
