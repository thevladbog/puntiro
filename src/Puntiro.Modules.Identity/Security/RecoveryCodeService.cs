using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Puntiro.Security;

namespace Puntiro.Modules.Identity.Security;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class GeneratedRecoveryCode
{
    public GeneratedRecoveryCode(SensitiveValue code, byte[] verifier)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(verifier);
        Code = code;
        Verifier = verifier;
    }

    [JsonIgnore]
    public SensitiveValue Code { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never), JsonIgnore]
    public byte[] Verifier { get; }

    public override string ToString() => nameof(GeneratedRecoveryCode);

    private string DebuggerDisplay => nameof(GeneratedRecoveryCode);
}

internal interface IRecoveryCodeService : IDisposable
{
    IReadOnlyList<GeneratedRecoveryCode> GenerateBatch();

    bool Verify(string presentedCode, ReadOnlySpan<byte> expectedVerifier);
}

internal sealed class RecoveryCodeService : IRecoveryCodeService
{
    private const int KeyLength = 32;
    private const int RecoveryValueLength = 16;
    private const int DefaultBatchSize = 10;
    private const int MaximumGenerationAttempts = DefaultBatchSize * 4;
    private const int GroupSize = 4;
    private const string Purpose = "Puntiro.Identity.RecoveryCode.v1";
    private static readonly byte[] PurposePrefix = Encoding.ASCII.GetBytes($"{Purpose}\0");
    private static readonly int EncodedLength = Base32.GetEncodedLength(RecoveryValueLength);
    private static readonly int GroupedLength = GetGroupedLength(EncodedLength);

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly byte[] _key;
    private readonly ISecretGenerator _secretGenerator;
    private bool _disposed;

    public RecoveryCodeService(byte[] key, ISecretGenerator secretGenerator)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(secretGenerator);
        if (key.Length != KeyLength)
        {
            throw new ArgumentException($"Recovery HMAC key must contain {KeyLength} bytes.", nameof(key));
        }

        _key = (byte[])key.Clone();
        _secretGenerator = secretGenerator;
    }

    public IReadOnlyList<GeneratedRecoveryCode> GenerateBatch()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var generated = new List<GeneratedRecoveryCode>(DefaultBatchSize);
        try
        {
            for (var attempt = 0;
                 attempt < MaximumGenerationAttempts && generated.Count < DefaultBatchSize;
                 attempt++)
            {
                var randomValue = new SensitiveBuffer<byte>(RecoveryValueLength);
                var normalized = new SensitiveBuffer<char>(EncodedLength);
                var grouped = new SensitiveBuffer<char>(GroupedLength);
                byte[]? verifier = null;
                SensitiveValue? code = null;
                try
                {
                    _secretGenerator.Fill(randomValue.Span);
                    Base32.Encode(randomValue.ReadOnlySpan, normalized.Span);
                    verifier = ComputeVerifier(normalized.ReadOnlySpan);

                    var duplicate = false;
                    foreach (var existing in generated)
                    {
                        duplicate |= CryptographicOperations.FixedTimeEquals(existing.Verifier, verifier);
                    }

                    if (duplicate)
                    {
                        continue;
                    }

                    Group(normalized.ReadOnlySpan, grouped.Span);
                    code = new SensitiveValue(grouped.ReadOnlySpan);
                    generated.Add(new GeneratedRecoveryCode(code, verifier));
                    code = null;
                    verifier = null;
                }
                finally
                {
                    code?.Dispose();
                    if (verifier is not null)
                    {
                        CryptographicOperations.ZeroMemory(verifier);
                    }

                    grouped.Dispose();
                    normalized.Dispose();
                    randomValue.Dispose();
                }
            }

            if (generated.Count != DefaultBatchSize)
            {
                throw new InvalidOperationException(
                    "Could not generate a unique recovery-code batch within the bounded attempt limit.");
            }

            return generated;
        }
        catch
        {
            ClearGenerated(generated);
            throw;
        }
    }

    public bool Verify(string presentedCode, ReadOnlySpan<byte> expectedVerifier)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var normalized = new SensitiveBuffer<char>(EncodedLength);
        try
        {
            if (expectedVerifier.Length != HMACSHA256.HashSizeInBytes ||
                !TryNormalize(presentedCode, normalized.Span))
            {
                return false;
            }

            var candidate = ComputeVerifier(normalized.ReadOnlySpan);
            try
            {
                return CryptographicOperations.FixedTimeEquals(candidate, expectedVerifier);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(candidate);
            }
        }
        finally
        {
            normalized.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(_key);
        _disposed = true;
    }

    private byte[] ComputeVerifier(ReadOnlySpan<char> normalizedCode)
    {
        if (normalizedCode.Length != EncodedLength)
        {
            throw new ArgumentException(
                $"Normalized recovery code must contain exactly {EncodedLength} characters.",
                nameof(normalizedCode));
        }

        var input = new SensitiveBuffer<byte>(PurposePrefix.Length + EncodedLength);
        try
        {
            PurposePrefix.CopyTo(input.Span);
            var bytesWritten = Encoding.ASCII.GetBytes(
                normalizedCode,
                input.Span[PurposePrefix.Length..]);
            if (bytesWritten != EncodedLength)
            {
                throw new InvalidOperationException("Recovery-code verifier input has an invalid length.");
            }

            return HMACSHA256.HashData(_key, input.ReadOnlySpan);
        }
        finally
        {
            input.Dispose();
        }
    }

    private static bool TryNormalize(string presentedCode, Span<char> normalized)
    {
        if (normalized.Length != EncodedLength)
        {
            throw new ArgumentException(
                $"Normalized recovery-code destination must contain exactly {EncodedLength} characters.",
                nameof(normalized));
        }

        if (presentedCode is null)
        {
            return false;
        }

        var count = 0;
        foreach (var character in presentedCode)
        {
            if (character == '-')
            {
                continue;
            }

            if (count == EncodedLength ||
                character is not (>= 'A' and <= 'Z' or >= '2' and <= '7'))
            {
                return false;
            }

            normalized[count++] = character;
        }

        if (count != EncodedLength)
        {
            return false;
        }

        return true;
    }

    private static void Group(ReadOnlySpan<char> normalized, Span<char> output)
    {
        var groupedLength = GetGroupedLength(normalized.Length);
        if (output.Length != groupedLength)
        {
            throw new ArgumentException(
                $"Grouped recovery-code destination must contain exactly {groupedLength} characters.",
                nameof(output));
        }

        var outputIndex = 0;
        for (var inputIndex = 0; inputIndex < normalized.Length; inputIndex++)
        {
            if (inputIndex > 0 && inputIndex % GroupSize == 0)
            {
                output[outputIndex++] = '-';
            }

            output[outputIndex++] = normalized[inputIndex];
        }
    }

    private static int GetGroupedLength(int normalizedLength) =>
        checked(normalizedLength + ((normalizedLength - 1) / GroupSize));

    private static void ClearGenerated(IEnumerable<GeneratedRecoveryCode> generated)
    {
        foreach (var item in generated)
        {
            item.Code.Dispose();
            CryptographicOperations.ZeroMemory(item.Verifier);
        }
    }
}
