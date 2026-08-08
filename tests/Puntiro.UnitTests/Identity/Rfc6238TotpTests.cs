using System.Text;
using Puntiro.Modules.Identity.Security;
using Puntiro.UnitTests.Security;
using Xunit;

namespace Puntiro.UnitTests.Identity;

public sealed class Rfc6238TotpTests
{
    private static readonly byte[] RfcSha1Secret = Encoding.ASCII.GetBytes("12345678901234567890");
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_234_567_890);

    [Theory]
    [InlineData(59L, "94287082")]
    [InlineData(1111111109L, "07081804")]
    [InlineData(1111111111L, "14050471")]
    [InlineData(1234567890L, "89005924")]
    [InlineData(2000000000L, "69279037")]
    [InlineData(20000000000L, "65353130")]
    public void Matches_rfc6238_sha1_vectors(long unixSeconds, string expected)
    {
        Assert.Equal(expected, Rfc6238Totp.Generate(RfcSha1Secret, unixSeconds, digits: 8));
    }

    [Fact]
    public void TryAccept_uses_the_injected_utc_clock()
    {
        var clock = new TestTimeProvider(Now);
        var totp = new Rfc6238Totp(clock, new TestSecretGenerator());
        var currentCounter = Now.ToUnixTimeSeconds() / 30;
        var code = Rfc6238Totp.Generate(RfcSha1Secret, Now.ToUnixTimeSeconds());

        Assert.True(totp.TryAccept(RfcSha1Secret, code, lastAcceptedCounter: null, out var accepted));
        Assert.Equal(currentCounter, accepted.Counter);
    }

    [Theory]
    [InlineData(-30L, -1L)]
    [InlineData(0L, 0L)]
    [InlineData(30L, 1L)]
    public void TryAccept_checks_previous_current_and_next_time_steps(long offsetSeconds, long expectedCounterOffset)
    {
        var totp = new Rfc6238Totp(new TestTimeProvider(Now), new TestSecretGenerator());
        var candidateTime = Now.AddSeconds(offsetSeconds);
        var code = Rfc6238Totp.Generate(RfcSha1Secret, candidateTime.ToUnixTimeSeconds());

        Assert.True(totp.TryAccept(RfcSha1Secret, code, Now, lastAcceptedCounter: null, out var accepted));
        Assert.Equal((Now.ToUnixTimeSeconds() / 30) + expectedCounterOffset, accepted.Counter);
    }

    [Fact]
    public void Totp_rejects_the_last_accepted_counter()
    {
        var totp = new Rfc6238Totp(new TestTimeProvider(Now), new TestSecretGenerator());
        var currentCounter = Now.ToUnixTimeSeconds() / 30;
        var currentCode = Rfc6238Totp.Generate(RfcSha1Secret, Now.ToUnixTimeSeconds());

        Assert.False(totp.TryAccept(
            RfcSha1Secret,
            currentCode,
            Now,
            lastAcceptedCounter: currentCounter,
            out _));
    }

    [Fact]
    public void TryAccept_rejects_a_previous_counter_after_a_newer_one_was_accepted()
    {
        var totp = new Rfc6238Totp(new TestTimeProvider(Now), new TestSecretGenerator());
        var currentCounter = Now.ToUnixTimeSeconds() / 30;
        var previousCode = Rfc6238Totp.Generate(RfcSha1Secret, Now.AddSeconds(-30).ToUnixTimeSeconds());

        Assert.False(totp.TryAccept(
            RfcSha1Secret,
            previousCode,
            Now,
            lastAcceptedCounter: currentCounter,
            out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12A456")]
    [InlineData(" 123456")]
    public void TryAccept_rejects_malformed_codes(string code)
    {
        var totp = new Rfc6238Totp(new TestTimeProvider(Now), new TestSecretGenerator());

        Assert.False(totp.TryAccept(RfcSha1Secret, code, lastAcceptedCounter: null, out _));
    }

    [Fact]
    public void GenerateSecret_returns_exactly_twenty_injected_random_bytes()
    {
        var expected = Enumerable.Range(0, 20).Select(static value => (byte)value).ToArray();
        var totp = new Rfc6238Totp(new TestTimeProvider(Now), new TestSecretGenerator(expected));

        Assert.Equal(expected, totp.GenerateSecret());
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
