using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Identity.Persistence;
using Puntiro.Modules.Identity.Contracts;
using Puntiro.Modules.Identity.Security;
using Puntiro.Modules.Identity.Services;
using Puntiro.Security;
using Xunit;

namespace Puntiro.UnitTests.Identity;

public sealed class IdentitySecretCleanupTests
{
    [Fact]
    public async Task Begin_does_not_acquire_following_secrets_when_password_hashing_fails()
    {
        var totp = new FaultingTotpService(throwOnGenerate: false);
        var recovery = new FaultingRecoveryCodeService(throwOnGenerate: false);
        await using var fixture = CreateFixture(new FaultingPasswordHasher(), totp, recovery);
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<SecretAcquisitionException>(() => fixture.Service.BeginOwnerAsync(
            Guid.CreateVersion7(),
            "cleanup@example.test",
            "password-marker",
            new IdentityAuditContext(null, "trace-cleanup-password"),
            cancellationToken));

        Assert.Equal(0, totp.GenerateCalls);
        Assert.Equal(0, recovery.GenerateCalls);
    }

    [Fact]
    public async Task Begin_clears_the_password_hash_when_totp_acquisition_fails()
    {
        var passwordHasher = new TrackingPasswordHasher();
        var totp = new FaultingTotpService(throwOnGenerate: true);
        var recovery = new FaultingRecoveryCodeService(throwOnGenerate: false);
        await using var fixture = CreateFixture(passwordHasher, totp, recovery);
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<SecretAcquisitionException>(() => fixture.Service.BeginOwnerAsync(
            Guid.CreateVersion7(),
            "cleanup@example.test",
            "password-marker",
            new IdentityAuditContext(null, "trace-cleanup-totp"),
            cancellationToken));

        Assert.All(passwordHasher.Salt, value => Assert.Equal(0, value));
        Assert.All(passwordHasher.Hash, value => Assert.Equal(0, value));
        Assert.Equal(0, recovery.GenerateCalls);
    }

    [Fact]
    public async Task Begin_clears_password_and_totp_when_recovery_acquisition_fails()
    {
        var passwordHasher = new TrackingPasswordHasher();
        var totp = new FaultingTotpService(throwOnGenerate: false);
        var recovery = new FaultingRecoveryCodeService(throwOnGenerate: true);
        await using var fixture = CreateFixture(passwordHasher, totp, recovery);
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<SecretAcquisitionException>(() => fixture.Service.BeginOwnerAsync(
            Guid.CreateVersion7(),
            "cleanup@example.test",
            "password-marker",
            new IdentityAuditContext(null, "trace-cleanup-recovery"),
            cancellationToken));

        Assert.All(passwordHasher.Salt, value => Assert.Equal(0, value));
        Assert.All(passwordHasher.Hash, value => Assert.Equal(0, value));
        Assert.All(totp.Secret, value => Assert.Equal(0, value));
        Assert.Equal(1, recovery.GenerateCalls);
    }

    private static CleanupFixture CreateFixture(
        IPasswordHasher passwordHasher,
        ITotpService totp,
        IRecoveryCodeService recovery)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=5432;Database=unused_cleanup;Username=unused_cleanup")
            .Options;
        var context = new IdentityDbContext(options);
        var keys = IdentityKeyOptions.ForTesting(
            "session-v1",
            Enumerable.Repeat((byte)0x51, 32).ToArray(),
            "recovery-v1",
            Enumerable.Repeat((byte)0x72, 32).ToArray());
        var secrets = new SystemSecretGenerator();
        var protector = new TotpSecretProtector(new EphemeralDataProtectionProvider());
        return new CleanupFixture(
            context,
            new IdentityProvisioningService(
                context,
                passwordHasher,
                totp,
                recovery,
                protector,
                TimeProvider.System,
                keys,
                secrets));
    }

    private sealed class CleanupFixture(
        IdentityDbContext context,
        IdentityProvisioningService service) : IAsyncDisposable
    {
        internal IdentityProvisioningService Service { get; } = service;

        public ValueTask DisposeAsync() => context.DisposeAsync();
    }

    private sealed class TrackingPasswordHasher : IPasswordHasher
    {
        internal byte[] Salt { get; } = Enumerable.Repeat((byte)0x31, 16).ToArray();
        internal byte[] Hash { get; } = Enumerable.Repeat((byte)0x42, 32).ToArray();

        public Task<PasswordHash> HashAsync(string password, CancellationToken cancellationToken) =>
            Task.FromResult(new PasswordHash(Salt, Hash, 19456, 2, 1, "argon2id"));

        public Task<PasswordVerification> VerifyAsync(
            string password,
            PasswordHash stored,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FaultingPasswordHasher : IPasswordHasher
    {
        public Task<PasswordHash> HashAsync(string password, CancellationToken cancellationToken) =>
            throw new SecretAcquisitionException();

        public Task<PasswordVerification> VerifyAsync(
            string password,
            PasswordHash stored,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FaultingTotpService(bool throwOnGenerate) : ITotpService
    {
        internal byte[] Secret { get; } = Enumerable.Repeat((byte)0x53, 20).ToArray();
        internal int GenerateCalls { get; private set; }

        public byte[] GenerateSecret()
        {
            GenerateCalls++;
            if (throwOnGenerate)
            {
                throw new SecretAcquisitionException();
            }

            return Secret;
        }

        public bool TryAccept(
            ReadOnlySpan<byte> secret,
            string code,
            long? lastAcceptedCounter,
            out AcceptedTotp accepted) =>
            throw new NotSupportedException();

        public bool TryAccept(
            ReadOnlySpan<byte> secret,
            string code,
            DateTimeOffset utcNow,
            long? lastAcceptedCounter,
            out AcceptedTotp accepted) =>
            throw new NotSupportedException();
    }

    private sealed class FaultingRecoveryCodeService(bool throwOnGenerate) : IRecoveryCodeService
    {
        internal int GenerateCalls { get; private set; }

        public IReadOnlyList<GeneratedRecoveryCode> GenerateBatch()
        {
            GenerateCalls++;
            if (throwOnGenerate)
            {
                throw new SecretAcquisitionException();
            }

            return [];
        }

        public bool Verify(string presentedCode, ReadOnlySpan<byte> expectedVerifier) =>
            throw new NotSupportedException();

        public void Dispose()
        {
        }
    }

    private sealed class SecretAcquisitionException : Exception;
}
