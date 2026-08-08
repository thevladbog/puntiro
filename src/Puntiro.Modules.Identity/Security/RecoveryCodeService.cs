using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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

    public SensitiveValue Code { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public byte[] Verifier { get; }

    public override string ToString() => nameof(GeneratedRecoveryCode);

    private string DebuggerDisplay => nameof(GeneratedRecoveryCode);
}

internal sealed class RecoveryCodeService : IDisposable
{
    private const int KeyLength = 32;
    private const int RecoveryValueLength = 16;
    private const int EncodedLength = 26;
    private const int DefaultBatchSize = 10;
    private const int GroupSize = 4;
    private const string Purpose = "Puntiro.Identity.RecoveryCode.v1";
    private static readonly byte[] PurposePrefix = Encoding.ASCII.GetBytes($"{Purpose}\0");

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
        for (var index = 0; index < DefaultBatchSize; index++)
        {
            var randomValue = new byte[RecoveryValueLength];
            try
            {
                _secretGenerator.Fill(randomValue);
                var normalized = Base32.Encode(randomValue);
                var verifier = ComputeVerifier(normalized);
                generated.Add(new GeneratedRecoveryCode(
                    new SensitiveValue(Group(normalized)),
                    verifier));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(randomValue);
            }
        }

        return generated;
    }

    public bool Verify(string presentedCode, ReadOnlySpan<byte> expectedVerifier)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (expectedVerifier.Length != HMACSHA256.HashSizeInBytes ||
            !TryNormalize(presentedCode, out var normalized))
        {
            return false;
        }

        var candidate = ComputeVerifier(normalized);
        try
        {
            return CryptographicOperations.FixedTimeEquals(candidate, expectedVerifier);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(candidate);
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

    private byte[] ComputeVerifier(string normalizedCode)
    {
        var input = new byte[PurposePrefix.Length + EncodedLength];
        try
        {
            PurposePrefix.CopyTo(input, 0);
            Encoding.ASCII.GetBytes(normalizedCode.AsSpan(), input.AsSpan(PurposePrefix.Length));
            return HMACSHA256.HashData(_key, input);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    private static bool TryNormalize(string presentedCode, out string normalized)
    {
        normalized = string.Empty;
        if (presentedCode is null)
        {
            return false;
        }

        Span<char> characters = stackalloc char[EncodedLength];
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

            characters[count++] = character;
        }

        if (count != EncodedLength)
        {
            return false;
        }

        normalized = new string(characters);
        return true;
    }

    private static string Group(string normalized)
    {
        var separatorCount = (normalized.Length - 1) / GroupSize;
        return string.Create(normalized.Length + separatorCount, normalized, static (output, input) =>
        {
            var outputIndex = 0;
            for (var inputIndex = 0; inputIndex < input.Length; inputIndex++)
            {
                if (inputIndex > 0 && inputIndex % GroupSize == 0)
                {
                    output[outputIndex++] = '-';
                }

                output[outputIndex++] = input[inputIndex];
            }
        });
    }
}
