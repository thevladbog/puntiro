using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

return AssemblyPolicyVerifier.Run(args);

internal static class AssemblyPolicyVerifier
{
    private const string AttributeTypeName = "System.Runtime.CompilerServices.InternalsVisibleToAttribute";
    private const string PolicyReference = "AGENTS.md#internal-access-policy";
    private static readonly Dictionary<string, TrustedAssemblyIdentity> BclAttributeAssemblies =
        new(StringComparer.Ordinal)
    {
        ["System.Private.CoreLib"] = new(new Version(10, 0, 0, 0), "7cec85d7bea7798e"),
        ["System.Runtime"] = new(new Version(10, 0, 0, 0), "b03f5f7f11d50a3a"),
    };
    private static readonly byte[] FriendConstructorSignature = [0x20, 0x01, 0x01, 0x0e];
    private static readonly string[] ApprovedFriends =
    [
        "Puntiro.IntegrationTests",
        "Puntiro.UnitTests",
    ];

    public static int Run(string[] arguments)
    {
        if (arguments.Length == 0 || arguments.Length % 2 != 0)
        {
            Console.Error.WriteLine(
                $"Usage: Puntiro.AssemblyPolicy <expected-assembly-name> <assembly.dll> [...] " +
                $"(see {PolicyReference})");
            return 2;
        }

        var inputs = new List<(string ExpectedName, string AssemblyPath)>();
        var expectedNames = new HashSet<string>(StringComparer.Ordinal);
        var assemblyPaths = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index += 2)
        {
            var expectedName = arguments[index];
            var assemblyPath = Path.GetFullPath(arguments[index + 1]);
            if (string.IsNullOrWhiteSpace(expectedName))
            {
                Console.Error.WriteLine($"expected assembly identity must not be empty (see {PolicyReference})");
                return 2;
            }
            if (!expectedNames.Add(expectedName))
            {
                Console.Error.WriteLine(
                    $"duplicate expected assembly identity: {expectedName} (see {PolicyReference})");
                return 2;
            }
            if (!assemblyPaths.Add(assemblyPath))
            {
                Console.Error.WriteLine(
                    $"duplicate protected assembly path: {assemblyPath} (see {PolicyReference})");
                return 2;
            }
            inputs.Add((expectedName, assemblyPath));
        }

        var failed = false;
        foreach (var (expectedName, assemblyPath) in inputs)
        {
            try
            {
                var assembly = ReadAssemblyMetadata(assemblyPath);
                if (!StringComparer.Ordinal.Equals(assembly.Name, expectedName))
                {
                    Console.Error.WriteLine(
                        $"{assemblyPath}: assembly identity mismatch; expected {expectedName}; " +
                        $"received {assembly.Name} (see {PolicyReference})");
                    failed = true;
                }

                var actualFriends = assembly.Friends
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                if (!actualFriends.SequenceEqual(ApprovedFriends, StringComparer.Ordinal))
                {
                    Console.Error.WriteLine(
                        $"{assemblyPath}: InternalsVisibleTo metadata must contain exactly " +
                        $"[{string.Join(", ", ApprovedFriends)}]; received " +
                        $"[{string.Join(", ", actualFriends)}] (see {PolicyReference})");
                    failed = true;
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or BadImageFormatException)
            {
                Console.Error.WriteLine($"{assemblyPath}: cannot inspect assembly metadata: {exception.Message} (see {PolicyReference})");
                failed = true;
            }
        }

        return failed ? 1 : 0;
    }

    private static AssemblyMetadataInfo ReadAssemblyMetadata(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
        {
            throw new BadImageFormatException("file has no managed metadata");
        }

        var metadata = peReader.GetMetadataReader();
        if (!metadata.IsAssembly)
        {
            throw new BadImageFormatException("managed metadata does not define an assembly");
        }

        var assembly = metadata.GetAssemblyDefinition();
        var friends = new List<string>();
        foreach (var handle in assembly.GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (!IsBclFriendAttribute(metadata, attribute.Constructor))
            {
                continue;
            }

            friends.Add(ReadSingleStringArgument(metadata, attribute.Value));
        }

        return new AssemblyMetadataInfo(metadata.GetString(assembly.Name), friends);
    }

    private static bool IsBclFriendAttribute(MetadataReader metadata, EntityHandle constructor)
    {
        if (constructor.Kind != HandleKind.MemberReference)
        {
            return false;
        }

        var member = metadata.GetMemberReference((MemberReferenceHandle)constructor);
        if (metadata.GetString(member.Name) != ".ctor"
            || !metadata.GetBlobBytes(member.Signature).SequenceEqual(FriendConstructorSignature))
        {
            return false;
        }

        var parent = member.Parent;
        if (parent.Kind != HandleKind.TypeReference)
        {
            return false;
        }

        var attributeType = metadata.GetTypeReference((TypeReferenceHandle)parent);
        if (FullName(metadata, attributeType) != AttributeTypeName
            || attributeType.ResolutionScope.Kind != HandleKind.AssemblyReference)
        {
            return false;
        }

        var assembly = metadata.GetAssemblyReference((AssemblyReferenceHandle)attributeType.ResolutionScope);
        if (!BclAttributeAssemblies.TryGetValue(
            metadata.GetString(assembly.Name),
            out var trustedIdentity))
        {
            return false;
        }

        return assembly.Version == trustedIdentity.Version
            && (assembly.Flags == 0 || assembly.Flags == AssemblyFlags.PublicKey)
            && metadata.GetString(assembly.Culture).Length == 0
            && ReadPublicKeyToken(metadata, assembly)
                .Equals(trustedIdentity.PublicKeyToken, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadPublicKeyToken(MetadataReader metadata, AssemblyReference assembly)
    {
        var keyOrToken = metadata.GetBlobBytes(assembly.PublicKeyOrToken);
        if (assembly.Flags != AssemblyFlags.PublicKey)
        {
            return Convert.ToHexString(keyOrToken);
        }

        var hash = SHA1.HashData(keyOrToken);
        var token = hash[^8..];
        Array.Reverse(token);
        return Convert.ToHexString(token);
    }

    private static string FullName(MetadataReader metadata, TypeReference type)
    {
        return JoinTypeName(metadata.GetString(type.Namespace), metadata.GetString(type.Name));
    }

    private static string JoinTypeName(string typeNamespace, string name)
    {
        return string.IsNullOrEmpty(typeNamespace) ? name : $"{typeNamespace}.{name}";
    }

    private static string ReadSingleStringArgument(MetadataReader metadata, BlobHandle value)
    {
        var blob = metadata.GetBlobReader(value);
        if (blob.ReadUInt16() != 1)
        {
            throw new BadImageFormatException("invalid custom-attribute prolog");
        }

        var friend = blob.ReadSerializedString()
            ?? throw new BadImageFormatException("friend assembly name is null");
        if (blob.ReadUInt16() != 0 || blob.RemainingBytes != 0)
        {
            throw new BadImageFormatException("unexpected named arguments in friend attribute");
        }

        return friend;
    }

    private sealed record AssemblyMetadataInfo(string Name, IReadOnlyList<string> Friends);

    private sealed record TrustedAssemblyIdentity(Version Version, string PublicKeyToken);
}
