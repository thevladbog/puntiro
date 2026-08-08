using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Puntiro.Security;

namespace Puntiro.Modules.Identity.Security;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed record PasswordHash(
    [property: DebuggerBrowsable(DebuggerBrowsableState.Never)] byte[] Salt,
    [property: DebuggerBrowsable(DebuggerBrowsableState.Never)] byte[] Hash,
    int MemoryKiB,
    int Iterations,
    int Parallelism,
    string Algorithm)
{
    public override string ToString() => nameof(PasswordHash);

    private string DebuggerDisplay => nameof(PasswordHash);
}

public enum PasswordVerification
{
    Failed,
    Valid,
    ValidNeedsRehash,
}

internal interface IPasswordHasher
{
    Task<PasswordHash> HashAsync(string password, CancellationToken cancellationToken);

    Task<PasswordVerification> VerifyAsync(
        string password,
        PasswordHash stored,
        CancellationToken cancellationToken);
}

internal sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const int CurrentMemoryKiB = 19456;
    private const int CurrentIterations = 2;
    private const int CurrentParallelism = 1;
    private const string CurrentAlgorithm = "argon2id";

    private readonly ISecretGenerator _secretGenerator;

    public PasswordHasher(ISecretGenerator secretGenerator)
    {
        ArgumentNullException.ThrowIfNull(secretGenerator);
        _secretGenerator = secretGenerator;
    }

    public async Task<PasswordHash> HashAsync(string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PasswordPolicy.Validate(password);

        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var salt = new byte[SaltLength];
        try
        {
            _secretGenerator.Fill(salt);
            var hash = await DeriveAsync(
                passwordBytes,
                salt,
                CurrentMemoryKiB,
                CurrentIterations,
                CurrentParallelism,
                cancellationToken).ConfigureAwait(false);

            return new PasswordHash(
                salt,
                hash,
                CurrentMemoryKiB,
                CurrentIterations,
                CurrentParallelism,
                CurrentAlgorithm);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(salt);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public async Task<PasswordVerification> VerifyAsync(
        string password,
        PasswordHash stored,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stored);
        cancellationToken.ThrowIfCancellationRequested();
        PasswordPolicy.Validate(password);

        if (!HasSafeStoredParameters(stored))
        {
            return PasswordVerification.Failed;
        }

        var passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[]? candidate = null;
        try
        {
            candidate = await DeriveAsync(
                passwordBytes,
                stored.Salt,
                stored.MemoryKiB,
                stored.Iterations,
                stored.Parallelism,
                cancellationToken).ConfigureAwait(false);

            if (!CryptographicOperations.FixedTimeEquals(candidate, stored.Hash))
            {
                return PasswordVerification.Failed;
            }

            return IsWeakerThanCurrent(stored)
                ? PasswordVerification.ValidNeedsRehash
                : PasswordVerification.Valid;
        }
        catch (ArgumentException)
        {
            return PasswordVerification.Failed;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            if (candidate is not null)
            {
                CryptographicOperations.ZeroMemory(candidate);
            }
        }
    }

    private static bool HasSafeStoredParameters(PasswordHash stored)
    {
        return string.Equals(stored.Algorithm, CurrentAlgorithm, StringComparison.Ordinal) &&
            stored.Salt is { Length: SaltLength } &&
            stored.Hash is { Length: HashLength } &&
            stored.MemoryKiB is >= 8 and <= CurrentMemoryKiB &&
            stored.Iterations is >= 1 and <= CurrentIterations &&
            stored.Parallelism is >= 1 and <= CurrentParallelism &&
            (long)stored.MemoryKiB >= 8L * stored.Parallelism;
    }

    private static bool IsWeakerThanCurrent(PasswordHash stored)
    {
        return stored.MemoryKiB < CurrentMemoryKiB ||
            stored.Iterations < CurrentIterations ||
            stored.Parallelism < CurrentParallelism;
    }

    private static async Task<byte[]> DeriveAsync(
        byte[] passwordBytes,
        byte[] salt,
        int memoryKiB,
        int iterations,
        int parallelism,
        CancellationToken cancellationToken)
    {
        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            MemorySize = memoryKiB,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };

        var hash = await argon2.GetBytesAsync(HashLength).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            CryptographicOperations.ZeroMemory(hash);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return hash;
    }
}
