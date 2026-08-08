using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
using Puntiro.Modules.Identity.Security;
using Puntiro.UnitTests.Security;
using Xunit;

namespace Puntiro.UnitTests.Identity;

public sealed class PasswordHasherTests
{
    private static readonly byte[] FirstSalt = Enumerable.Range(0, 16).Select(static value => (byte)value).ToArray();
    private static readonly byte[] SecondSalt = Enumerable.Range(16, 16).Select(static value => (byte)value).ToArray();

    [Fact]
    public async Task Hash_uses_exact_argon2id_policy_and_unique_salt()
    {
        var hasher = new PasswordHasher(new TestSecretGenerator(FirstSalt, SecondSalt));

        var first = await hasher.HashAsync("correct horse battery staple", TestCancellation);
        var second = await hasher.HashAsync("correct horse battery staple", TestCancellation);

        Assert.Equal(
            (19456, 2, 1, "argon2id"),
            (first.MemoryKiB, first.Iterations, first.Parallelism, first.Algorithm));
        Assert.Equal(16, first.Salt.Length);
        Assert.Equal(32, first.Hash.Length);
        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public async Task Verify_accepts_the_password_and_rejects_a_different_password()
    {
        var hasher = new PasswordHasher(new TestSecretGenerator(FirstSalt));
        var stored = await hasher.HashAsync("correct horse battery staple", TestCancellation);

        Assert.Equal(
            PasswordVerification.Valid,
            await hasher.VerifyAsync("correct horse battery staple", stored, TestCancellation));
        Assert.Equal(
            PasswordVerification.Failed,
            await hasher.VerifyAsync("different horse battery staple", stored, TestCancellation));
    }

    [Fact]
    public async Task Verify_does_not_normalize_the_password()
    {
        var hasher = new PasswordHasher(new TestSecretGenerator(FirstSalt));
        var stored = await hasher.HashAsync("é-correct-horse", TestCancellation);

        Assert.Equal(
            PasswordVerification.Valid,
            await hasher.VerifyAsync("é-correct-horse", stored, TestCancellation));
        Assert.Equal(
            PasswordVerification.Failed,
            await hasher.VerifyAsync("é-correct-horse", stored, TestCancellation));
    }

    [Fact]
    public async Task Verify_returns_failed_for_ordinary_invalid_candidates()
    {
        var stored = new PasswordHash(FirstSalt, new byte[32], 19456, 2, 1, "argon2id");
        var hasher = new PasswordHasher(new TestSecretGenerator());
        string[] candidates = [string.Empty, "short", "invalid\ud800"];

        foreach (var candidate in candidates)
        {
            Assert.Equal(
                PasswordVerification.Failed,
                await hasher.VerifyAsync(candidate, stored, TestCancellation));
        }
    }

    [Fact]
    public async Task Verify_accepts_historical_candidates_outside_current_creation_scalar_policy()
    {
        var hasher = new PasswordHasher(new TestSecretGenerator());
        string[] historicalPasswords = ["short", new string('x', 200)];

        foreach (var password in historicalPasswords)
        {
            var hash = await DeriveAsync(password, FirstSalt, memoryKiB: 19456, iterations: 2, parallelism: 1);
            var stored = new PasswordHash(FirstSalt, hash, 19456, 2, 1, "argon2id");

            Assert.Equal(
                PasswordVerification.Valid,
                await hasher.VerifyAsync(password, stored, TestCancellation));
        }
    }

    [Fact]
    public async Task Verify_fails_closed_before_argon_for_candidates_above_hard_bounds()
    {
        var stored = new PasswordHash(FirstSalt, new byte[32], 19456, 2, 1, "argon2id");
        var hasher = new PasswordHasher(new TestSecretGenerator());

        Assert.Equal(
            PasswordVerification.Failed,
            await hasher.VerifyAsync(new string('x', 1025), stored, TestCancellation));
    }

    [Fact]
    public async Task Verify_rejects_programmer_null_and_honors_cancellation()
    {
        var stored = new PasswordHash(FirstSalt, new byte[32], 19456, 2, 1, "argon2id");
        var hasher = new PasswordHasher(new TestSecretGenerator());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => hasher.VerifyAsync(null!, stored, TestCancellation));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => hasher.VerifyAsync("historical", stored, cancellation.Token));
    }

    [Fact]
    public async Task Verify_returns_needs_rehash_for_a_valid_weaker_hash()
    {
        const string password = "correct horse battery staple";
        var weakHash = await DeriveAsync(password, FirstSalt, memoryKiB: 8192, iterations: 1, parallelism: 1);
        var stored = new PasswordHash(FirstSalt, weakHash, 8192, 1, 1, "argon2id");
        var hasher = new PasswordHasher(new TestSecretGenerator());

        Assert.Equal(
            PasswordVerification.ValidNeedsRehash,
            await hasher.VerifyAsync(password, stored, TestCancellation));
    }

    [Theory]
    [InlineData("argon2i", 19456, 2, 1)]
    [InlineData("argon2id", 0, 2, 1)]
    [InlineData("argon2id", 19456, 0, 1)]
    [InlineData("argon2id", 19456, 2, 0)]
    [InlineData("argon2id", 1048577, 2, 1)]
    [InlineData("argon2id", 19456, 101, 1)]
    [InlineData("argon2id", 19456, 2, 65)]
    public async Task Verify_fails_closed_for_unsupported_or_unsafe_stored_parameters(
        string algorithm,
        int memoryKiB,
        int iterations,
        int parallelism)
    {
        var stored = new PasswordHash(FirstSalt, new byte[32], memoryKiB, iterations, parallelism, algorithm);
        var hasher = new PasswordHasher(new TestSecretGenerator());

        Assert.Equal(
            PasswordVerification.Failed,
            await hasher.VerifyAsync("correct horse battery staple", stored, TestCancellation));
    }

    [Fact]
    public async Task Verify_fails_closed_for_invalid_salt_or_hash_lengths()
    {
        var hasher = new PasswordHasher(new TestSecretGenerator());
        var invalidSalt = new PasswordHash(new byte[15], new byte[32], 19456, 2, 1, "argon2id");
        var invalidHash = new PasswordHash(new byte[16], new byte[31], 19456, 2, 1, "argon2id");

        Assert.Equal(PasswordVerification.Failed, await hasher.VerifyAsync("correct horse battery staple", invalidSalt, TestCancellation));
        Assert.Equal(PasswordVerification.Failed, await hasher.VerifyAsync("correct horse battery staple", invalidHash, TestCancellation));
    }

    [Fact]
    public void Password_policy_counts_unicode_scalars_without_composition_rules()
    {
        PasswordPolicy.Validate("12345678901🙂");
        PasswordPolicy.Validate(new string('x', 128));

        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate("1234567890🙂"));
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(new string('x', 129)));
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate("12345678901\ud800"));
    }

    [Fact]
    public void Password_policy_rejects_more_than_1024_utf8_bytes()
    {
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(string.Concat(Enumerable.Repeat("🙂", 257))));
    }

    [Fact]
    public async Task Hash_honors_pre_cancelled_requests_before_generating_a_salt()
    {
        var hasher = new PasswordHasher(new TestSecretGenerator());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => hasher.HashAsync("correct horse battery staple", cancellation.Token));
    }

    [Fact]
    public async Task Password_hash_string_representation_does_not_expose_material()
    {
        var hasher = new PasswordHasher(new TestSecretGenerator(FirstSalt));
        var stored = await hasher.HashAsync("correct horse battery staple", TestCancellation);

        Assert.Equal(nameof(PasswordHash), stored.ToString());
    }

    [Fact]
    public void Password_hash_json_contains_only_non_secret_policy_metadata()
    {
        var stored = new PasswordHash(FirstSalt, new byte[32], 19456, 2, 1, "argon2id");

        var json = JsonSerializer.Serialize(stored);

        Assert.Equal(
            "{\"MemoryKiB\":19456,\"Iterations\":2,\"Parallelism\":1,\"Algorithm\":\"argon2id\"}",
            json);
    }

    private static async Task<byte[]> DeriveAsync(
        string password,
        byte[] salt,
        int memoryKiB,
        int iterations,
        int parallelism)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                MemorySize = memoryKiB,
                Iterations = iterations,
                DegreeOfParallelism = parallelism,
            };

            return await argon2.GetBytesAsync(32);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;
}
