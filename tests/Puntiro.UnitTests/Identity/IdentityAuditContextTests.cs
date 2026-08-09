using Puntiro.Modules.Identity.Contracts;
using Xunit;

namespace Puntiro.UnitTests.Identity;

public sealed class IdentityAuditContextTests
{
    [Theory]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("contains/slash")]
    [InlineData("contains\nnewline")]
    public void Constructor_rejects_unsafe_trace_ids(string traceId)
    {
        Assert.Throws<ArgumentException>(() => new IdentityAuditContext(null, traceId));
    }

    [Fact]
    public void Constructor_rejects_an_empty_actor_and_overlong_trace()
    {
        Assert.Throws<ArgumentException>(() =>
            new IdentityAuditContext(Guid.Empty, "trace-valid-001"));
        Assert.Throws<ArgumentException>(() =>
            new IdentityAuditContext(null, new string('a', 129)));
    }

    [Fact]
    public void Constructor_accepts_nullable_actor_and_bounded_safe_trace()
    {
        var unauthenticated = new IdentityAuditContext(null, "trace:login-001");
        var authenticated = new IdentityAuditContext(Guid.CreateVersion7(), new string('a', 128));

        Assert.Null(unauthenticated.ActorUserId);
        Assert.Equal("trace:login-001", unauthenticated.TraceId);
        Assert.NotNull(authenticated.ActorUserId);
        Assert.Equal(128, authenticated.TraceId.Length);
    }
}
