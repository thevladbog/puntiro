using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Puntiro.Modules.Identity.Security;
using Puntiro.Security;
using Puntiro.UnitTests.Security;
using Xunit;

namespace Puntiro.UnitTests.Identity;

public sealed class RecoveryCodeServiceTests
{
    private static readonly byte[] RecoveryKey = Enumerable.Range(32, 32).Select(static value => (byte)value).ToArray();

    [Fact]
    public void GenerateBatch_returns_ten_grouped_codes_and_only_hmac_verifiers()
    {
        using var service = new RecoveryCodeService(
            RecoveryKey,
            new TestSecretGenerator(CreateRecoveryValues()));

        var batch = service.GenerateBatch();

        Assert.Equal(10, batch.Count);
        Assert.Equal(10, batch.Select(static item => item.Code.Reveal()).Distinct(StringComparer.Ordinal).Count());
        Assert.All(batch, item =>
        {
            Assert.Matches(
                new Regex("^[A-Z2-7]{4}(?:-[A-Z2-7]{4}){5}-[A-Z2-7]{2}$", RegexOptions.CultureInvariant),
                item.Code.Reveal());
            Assert.Equal(32, item.Verifier.Length);
            Assert.True(service.Verify(item.Code.Reveal(), item.Verifier));
            Assert.True(service.Verify(item.Code.Reveal().Replace("-", string.Empty, StringComparison.Ordinal), item.Verifier));
        });
    }

    [Fact]
    public void GenerateBatch_retries_duplicate_draws_until_the_batch_is_unique()
    {
        var uniqueValues = CreateRecoveryValues();
        var draws = new[] { uniqueValues[0], uniqueValues[0] }
            .Concat(uniqueValues.Skip(1))
            .ToArray();
        using var service = new RecoveryCodeService(
            RecoveryKey,
            new TestSecretGenerator(draws));

        var batch = service.GenerateBatch();

        Assert.Equal(10, batch.Count);
        Assert.Equal(10, batch.Select(static item => item.Code.Reveal()).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void GenerateBatch_fails_closed_after_bounded_permanent_duplicates()
    {
        using var service = new RecoveryCodeService(
            RecoveryKey,
            new RepeatingSecretGenerator(CreateRecoveryValues()[0]));

        Assert.Throws<InvalidOperationException>(() => service.GenerateBatch());
    }

    [Fact]
    public void GenerateBatch_call_graph_has_no_immutable_raw_code_stage()
    {
        var root = Assert.IsAssignableFrom<MethodInfo>(
            typeof(RecoveryCodeService).GetMethod(nameof(RecoveryCodeService.GenerateBatch)));
        var reachableMethods = GetReachableMethods(root);
        var reachableCalls = reachableMethods.SelectMany(GetDirectCalls).ToArray();
        var sensitiveBufferConstructions = reachableCalls.Count(
            static method => IsSensitiveBufferMember(method) && method is ConstructorInfo);
        var sensitiveBufferDisposals = reachableCalls.Count(
            static method => IsSensitiveBufferMember(method) &&
                method.Name == nameof(IDisposable.Dispose));

        Assert.DoesNotContain(reachableMethods, static method => ContainsOpCode(method, OpCodes.Newarr));
        Assert.True(sensitiveBufferConstructions > 0);
        Assert.Equal(sensitiveBufferConstructions, sensitiveBufferDisposals);
        Assert.Contains(
            reachableCalls,
            static method => method.DeclaringType == typeof(Base32) &&
                method.Name == nameof(Base32.Encode));
        Assert.DoesNotContain(
            reachableCalls,
            static method => method.DeclaringType is not null &&
                (method.DeclaringType == typeof(Base32) ||
                 method.DeclaringType == typeof(SensitiveValue) ||
                 method.DeclaringType == typeof(RecoveryCodeService)) &&
                (method is MethodInfo { ReturnType: var returnType } && returnType == typeof(string) ||
                 method.GetParameters().Any(static parameter => parameter.ParameterType == typeof(string))));
    }

    [Fact]
    public void Verify_rejects_wrong_malformed_or_noncanonical_codes()
    {
        using var service = new RecoveryCodeService(
            RecoveryKey,
            new TestSecretGenerator(CreateRecoveryValues()));
        var generated = service.GenerateBatch()[0];

        Assert.False(service.Verify("AAAA-AAAA-AAAA-AAAA-AAAA-AAAA-AA", generated.Verifier));
        Assert.False(service.Verify(generated.Code.Reveal().ToLowerInvariant(), generated.Verifier));
        Assert.False(service.Verify($" {generated.Code.Reveal()}", generated.Verifier));
        Assert.False(service.Verify(generated.Code.Reveal(), new byte[31]));
    }

    [Fact]
    public void Verifier_is_bound_to_the_recovery_key_and_purpose()
    {
        using var issuer = new RecoveryCodeService(
            RecoveryKey,
            new TestSecretGenerator(CreateRecoveryValues()));
        using var otherKey = new RecoveryCodeService(
            Enumerable.Repeat((byte)0xa5, 32).ToArray(),
            new TestSecretGenerator());
        var generated = issuer.GenerateBatch()[0];
        var normalized = generated.Code.Reveal().Replace("-", string.Empty, StringComparison.Ordinal);
        var normalizedBytes = Encoding.ASCII.GetBytes(normalized);
        var plainHmac = HMACSHA256.HashData(RecoveryKey, normalizedBytes);

        try
        {
            Assert.False(otherKey.Verify(generated.Code.Reveal(), generated.Verifier));
            Assert.False(CryptographicOperations.FixedTimeEquals(plainHmac, generated.Verifier));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(normalizedBytes);
            CryptographicOperations.ZeroMemory(plainHmac);
        }
    }

    [Fact]
    public void Sensitive_values_and_generated_records_are_redacted()
    {
        var secret = new SensitiveValue("do-not-log-this-value");
        using var service = new RecoveryCodeService(
            RecoveryKey,
            new TestSecretGenerator(CreateRecoveryValues()));
        var generated = service.GenerateBatch()[0];

        Assert.Equal("[REDACTED]", secret.ToString());
        Assert.Equal("do-not-log-this-value", secret.Reveal());
        Assert.DoesNotContain(generated.Code.Reveal(), generated.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Sensitive_and_generated_values_have_fail_closed_json_contracts()
    {
        var sensitive = new SensitiveValue("do-not-serialize");
        using var service = new RecoveryCodeService(
            RecoveryKey,
            new TestSecretGenerator(CreateRecoveryValues()));
        var generated = service.GenerateBatch()[0];

        Assert.Equal("\"[REDACTED]\"", JsonSerializer.Serialize(sensitive));
        Assert.Equal("{}", JsonSerializer.Serialize(generated));

        sensitive.Dispose();
        Assert.Equal("\"[REDACTED]\"", JsonSerializer.Serialize(sensitive));
    }

    [Fact]
    public void Sensitive_value_debugger_proxy_exposes_only_redacted_text()
    {
        using var sensitive = new SensitiveValue("do-not-show-in-debugger");
        var proxyAttribute = Assert.IsType<DebuggerTypeProxyAttribute>(
            Assert.Single(typeof(SensitiveValue).GetCustomAttributes(typeof(DebuggerTypeProxyAttribute), inherit: false)));
        var proxyType = Type.GetType(proxyAttribute.ProxyTypeName);
        Assert.NotNull(proxyType);
        var proxy = Activator.CreateInstance(
            proxyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [sensitive],
            culture: null);
        Assert.NotNull(proxy);

        var values = proxyType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.GetValue(proxy));

        Assert.All(values, value => Assert.Equal("[REDACTED]", value));
    }

    [Fact]
    public void Sensitive_value_owns_a_mutable_copy_and_disposal_is_idempotent()
    {
        char[] callerBuffer = ['s', 'e', 'c', 'r', 'e', 't'];
        var sensitive = new SensitiveValue(callerBuffer.AsSpan());
        Array.Clear(callerBuffer);

        Assert.Equal("secret", sensitive.Use(static value => new string(value)));

        sensitive.Dispose();
        sensitive.Dispose();

        Assert.Equal("[REDACTED]", sensitive.ToString());
        Assert.Throws<ObjectDisposedException>(() => sensitive.Reveal());
        Assert.Throws<ObjectDisposedException>(() => sensitive.Use(static value => value.Length));
    }

    [Fact]
    public void Disposed_service_fails_closed()
    {
        var service = new RecoveryCodeService(
            RecoveryKey,
            new TestSecretGenerator(CreateRecoveryValues()));
        var generated = service.GenerateBatch()[0];

        service.Dispose();

        Assert.Throws<ObjectDisposedException>(() => service.Verify(generated.Code.Reveal(), generated.Verifier));
        Assert.Throws<ObjectDisposedException>(() => service.GenerateBatch());
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Base32_matches_rfc4648_unpadded_vectors(string input, string expected)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(input);

        Assert.Equal(expected, Base32.Encode(bytes));

        Span<byte> decoded = stackalloc byte[bytes.Length];
        Assert.True(Base32.TryDecode(expected, decoded, out var bytesWritten));
        Assert.Equal(bytes, decoded[..bytesWritten].ToArray());
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Base32_span_encoder_matches_rfc4648_without_allocating_a_result_string(
        string input,
        string expected)
    {
        var bytes = Encoding.ASCII.GetBytes(input);
        var output = new char[Base32.GetEncodedLength(bytes.Length)];

        Base32.Encode(bytes, output);

        Assert.Equal(expected.ToCharArray(), output);
    }

    [Fact]
    public void Base32_span_encoder_requires_the_exact_destination_length()
    {
        byte[] value = [0xff];

        Assert.Equal(2, Base32.GetEncodedLength(value.Length));
        Assert.Throws<ArgumentException>(() => Base32.Encode(value, new char[1]));
        Assert.Throws<ArgumentException>(() => Base32.Encode(value, new char[3]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Base32.GetEncodedLength(-1));
        Assert.Throws<OverflowException>(() => Base32.GetEncodedLength(int.MaxValue));
    }

    [Fact]
    public void Sensitive_buffer_disposal_zeroes_owned_storage_and_is_idempotent()
    {
        byte[] bytes = [1, 2, 3, 4];
        char[] characters = ['A', 'B', 'C'];
        var byteBuffer = new SensitiveBuffer<byte>(bytes);
        var characterBuffer = new SensitiveBuffer<char>(characters);

        Assert.Equal(bytes, byteBuffer.ReadOnlySpan.ToArray());
        Assert.Equal(characters, characterBuffer.ReadOnlySpan.ToArray());

        byteBuffer.Dispose();
        byteBuffer.Dispose();
        characterBuffer.Dispose();
        characterBuffer.Dispose();

        Assert.All(bytes, static value => Assert.Equal(0, value));
        Assert.All(characters, static value => Assert.Equal('\0', value));
        Assert.Throws<ObjectDisposedException>(() => byteBuffer.ReadOnlySpan.Length);
        Assert.Throws<ObjectDisposedException>(() => characterBuffer.Span.Length);
    }

    [Fact]
    public void Sensitive_buffer_allocates_owned_storage_with_a_strict_length()
    {
        using var buffer = new SensitiveBuffer<byte>(4);

        Assert.Equal(4, buffer.Span.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SensitiveBuffer<byte>(-1));
    }

    [Theory]
    [InlineData("M=")]
    [InlineData("mY")]
    [InlineData("M1")]
    [InlineData("MZ")]
    public void Base32_rejects_malformed_or_noncanonical_encodings(string input)
    {
        Span<byte> decoded = stackalloc byte[32];

        Assert.False(Base32.TryDecode(input, decoded, out _));
    }

    [Fact]
    public void Base64Url_round_trips_without_padding_and_rejects_noncanonical_input()
    {
        byte[] bytes = [0xfb, 0xef, 0xff];
        var encoded = Base64Url.Encode(bytes);

        Assert.Equal("--__", encoded);
        Span<byte> decoded = stackalloc byte[3];
        Assert.True(Base64Url.TryDecode(encoded, decoded, out var bytesWritten));
        Assert.Equal(bytes, decoded[..bytesWritten].ToArray());
        Assert.False(Base64Url.TryDecode("Zh", decoded, out _));
        Assert.False(Base64Url.TryDecode("Zg==", decoded, out _));
    }

    private static byte[][] CreateRecoveryValues()
    {
        return Enumerable.Range(0, 10)
            .Select(index => Enumerable.Range(index * 16, 16).Select(static value => (byte)value).ToArray())
            .ToArray();
    }

    private static IReadOnlyList<MethodInfo> GetReachableMethods(MethodInfo root)
    {
        var pending = new Queue<MethodInfo>();
        var visited = new HashSet<MethodInfo>();
        pending.Enqueue(root);

        while (pending.TryDequeue(out var method))
        {
            if (!visited.Add(method))
            {
                continue;
            }

            foreach (var called in GetDirectCalls(method))
            {
                if (called is MethodInfo nested &&
                    nested.DeclaringType == typeof(RecoveryCodeService))
                {
                    pending.Enqueue(nested);
                }
            }
        }

        return visited.ToArray();
    }

    private static bool ContainsOpCode(MethodInfo method, OpCode expected)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        Assert.NotNull(il);

        var offset = 0;
        while (offset < il.Length)
        {
            var opCode = ReadOpCode(il, ref offset);
            if (opCode == expected)
            {
                return true;
            }

            offset += GetOperandSize(opCode.OperandType, il, offset);
        }

        return false;
    }

    private static bool IsSensitiveBufferMember(MethodBase method)
    {
        return method.DeclaringType is { IsGenericType: true } declaringType &&
            declaringType.GetGenericTypeDefinition() == typeof(SensitiveBuffer<>);
    }

    private static IEnumerable<MethodBase> GetDirectCalls(MethodInfo method)
    {
        var body = method.GetMethodBody();
        var il = body?.GetILAsByteArray();
        Assert.NotNull(il);

        var offset = 0;
        while (offset < il.Length)
        {
            var opCode = ReadOpCode(il, ref offset);
            if (opCode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, offset);
                yield return method.Module.ResolveMethod(
                    token,
                    method.DeclaringType?.GetGenericArguments(),
                    method.GetGenericArguments())!;
            }

            offset += GetOperandSize(opCode.OperandType, il, offset);
        }
    }

    private static OpCode ReadOpCode(byte[] il, ref int offset)
    {
        var first = il[offset++];
        if (first != 0xfe)
        {
            return SingleByteOpCodes[first];
        }

        return MultiByteOpCodes[il[offset++]];
    }

    private static int GetOperandSize(OperandType operandType, byte[] il, int offset)
    {
        return operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or
                OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or
                OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + (BitConverter.ToInt32(il, offset) * 4),
            _ => throw new InvalidOperationException($"Unexpected IL operand type {operandType}."),
        };
    }

    private static readonly OpCode[] SingleByteOpCodes = CreateOpCodeTable(multiByte: false);
    private static readonly OpCode[] MultiByteOpCodes = CreateOpCodeTable(multiByte: true);

    private static OpCode[] CreateOpCodeTable(bool multiByte)
    {
        var table = new OpCode[256];
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var opCode = Assert.IsType<OpCode>(field.GetValue(null));
            var value = unchecked((ushort)opCode.Value);
            if ((value > byte.MaxValue) == multiByte)
            {
                table[value & byte.MaxValue] = opCode;
            }
        }

        return table;
    }
}
