using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

return AssemblyPolicyVerifier.Run(args);

internal static class AssemblyPolicyVerifier
{
    private const string AttributeTypeName = "System.Runtime.CompilerServices.InternalsVisibleToAttribute";
    private const string PolicyReference = "AGENTS.md#internal-access-policy";
    private static readonly string[] ApprovedFriends =
    [
        "Puntiro.IntegrationTests",
        "Puntiro.UnitTests",
    ];

    public static int Run(string[] assemblyPaths)
    {
        if (assemblyPaths.Length == 0)
        {
            Console.Error.WriteLine($"Usage: Puntiro.AssemblyPolicy <assembly.dll> [...] (see {PolicyReference})");
            return 2;
        }

        var failed = false;
        foreach (var assemblyPath in assemblyPaths)
        {
            try
            {
                var actualFriends = ReadFriendAssemblies(assemblyPath)
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
            catch (Exception exception) when (exception is IOException or BadImageFormatException)
            {
                Console.Error.WriteLine($"{assemblyPath}: cannot inspect assembly metadata: {exception.Message} (see {PolicyReference})");
                failed = true;
            }
        }

        return failed ? 1 : 0;
    }

    private static IEnumerable<string> ReadFriendAssemblies(string assemblyPath)
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

        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (GetAttributeTypeName(metadata, attribute.Constructor) != AttributeTypeName)
            {
                continue;
            }

            yield return ReadSingleStringArgument(metadata, attribute.Value);
        }
    }

    private static string? GetAttributeTypeName(MetadataReader metadata, EntityHandle constructor)
    {
        return constructor.Kind switch
        {
            HandleKind.MemberReference => GetTypeName(metadata, metadata.GetMemberReference((MemberReferenceHandle)constructor).Parent),
            HandleKind.MethodDefinition => GetTypeName(
                metadata,
                metadata.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType()),
            _ => null,
        };
    }

    private static string? GetTypeName(MetadataReader metadata, EntityHandle type)
    {
        return type.Kind switch
        {
            HandleKind.TypeReference => FullName(metadata, metadata.GetTypeReference((TypeReferenceHandle)type)),
            HandleKind.TypeDefinition => FullName(metadata, metadata.GetTypeDefinition((TypeDefinitionHandle)type)),
            _ => null,
        };
    }

    private static string FullName(MetadataReader metadata, TypeReference type)
    {
        return JoinTypeName(metadata.GetString(type.Namespace), metadata.GetString(type.Name));
    }

    private static string FullName(MetadataReader metadata, TypeDefinition type)
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
}
