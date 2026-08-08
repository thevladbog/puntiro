using Puntiro.Modules.Tenancy.Contracts;
using Xunit;

namespace Puntiro.UnitTests.Tenancy;

public sealed class TenancyAuditContextTests
{
    [Fact]
    public void Constructor_rejects_an_empty_actor_id()
    {
        Assert.Throws<ArgumentException>(
            () => new TenancyAuditContext(Guid.Empty, "trace-valid-001"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("trace\nforged")]
    [InlineData("trace/value")]
    public void Constructor_rejects_an_unsafe_trace_id(string traceId)
    {
        Assert.Throws<ArgumentException>(
            () => new TenancyAuditContext(Guid.CreateVersion7(), traceId));
    }

    [Fact]
    public void Constructor_rejects_an_overlong_trace_id()
    {
        Assert.Throws<ArgumentException>(
            () => new TenancyAuditContext(Guid.CreateVersion7(), new string('a', 129)));
    }

    [Fact]
    public void Constructor_accepts_a_trace_id_at_the_maximum_length()
    {
        var context = new TenancyAuditContext(Guid.CreateVersion7(), new string('a', 128));

        Assert.Equal(128, context.TraceId.Length);
    }
}
