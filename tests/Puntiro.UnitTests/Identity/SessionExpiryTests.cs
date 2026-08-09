using Puntiro.Modules.Identity.Services;
using Xunit;

namespace Puntiro.UnitTests.Identity;

public sealed class SessionExpiryTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(29, true)]
    [InlineData(30, false)]
    public void Idle_expiry_is_strict_at_the_thirty_minute_boundary(
        int inactiveMinutes,
        bool expectedValid)
    {
        Assert.Equal(
            expectedValid,
            SessionExpiryPolicy.IsValid(
                CreatedAt.AddMinutes(inactiveMinutes),
                CreatedAt.AddMinutes(30),
                CreatedAt.AddHours(12),
                revokedAtUtc: null));
    }

    [Theory]
    [InlineData(719, true)]
    [InlineData(720, false)]
    public void Absolute_expiry_is_strict_at_the_twelve_hour_boundary(
        int elapsedMinutes,
        bool expectedValid)
    {
        Assert.Equal(
            expectedValid,
            SessionExpiryPolicy.IsValid(
                CreatedAt.AddMinutes(elapsedMinutes),
                CreatedAt.AddHours(13),
                CreatedAt.AddHours(12),
                revokedAtUtc: null));
    }

    [Fact]
    public void Revocation_invalidates_a_session_immediately()
    {
        Assert.False(SessionExpiryPolicy.IsValid(
            CreatedAt,
            CreatedAt.AddMinutes(30),
            CreatedAt.AddHours(12),
            CreatedAt));
    }

    [Fact]
    public void Idle_extension_never_crosses_absolute_expiry()
    {
        Assert.Equal(
            CreatedAt.AddHours(12),
            SessionExpiryPolicy.NextIdleExpiry(
                CreatedAt.AddHours(11).AddMinutes(50),
                CreatedAt.AddHours(12)));
    }
}
