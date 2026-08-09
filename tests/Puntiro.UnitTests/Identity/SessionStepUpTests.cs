using Puntiro.Modules.Identity.Domain;
using Xunit;

namespace Puntiro.UnitTests.Identity;

public sealed class SessionStepUpTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Recording_step_up_is_monotonic()
    {
        var session = CreateSession();
        var newer = CreatedAt.AddMinutes(2);

        session.RecordStepUp(newer);
        session.RecordStepUp(CreatedAt.AddMinutes(1));

        Assert.Equal(newer, session.SecondFactorVerifiedAtUtc);
    }

    private static AdminSession CreateSession() => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        new byte[32],
        "v1",
        Guid.CreateVersion7(),
        1,
        Guid.CreateVersion7(),
        CreatedAt,
        CreatedAt.AddMinutes(30),
        CreatedAt.AddHours(12),
        secondFactorVerifiedAtUtc: null);
}
