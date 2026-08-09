using System.Collections.Frozen;

namespace Puntiro.Modules.Integrations.Domain;

public enum IntegrationScope
{
    ShipmentsRead,
    ShipmentsWrite
}

internal static class IntegrationScopes
{
    internal const string ShipmentsReadValue = "shipments.read";
    internal const string ShipmentsWriteValue = "shipments.write";

    internal static FrozenSet<IntegrationScope> Normalize(
        IReadOnlySet<IntegrationScope> scopes,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(scopes, parameterName);
        if (scopes.Count is 0 or > 2 || scopes.Any(static scope => !IsApproved(scope)))
        {
            throw new ArgumentException(
                "At least one approved integration scope is required.",
                parameterName);
        }

        return scopes.ToFrozenSet();
    }

    internal static bool IsApproved(IntegrationScope scope) =>
        scope is IntegrationScope.ShipmentsRead or IntegrationScope.ShipmentsWrite;

    internal static string ToDatabaseValue(IntegrationScope scope) =>
        scope switch
        {
            IntegrationScope.ShipmentsRead => ShipmentsReadValue,
            IntegrationScope.ShipmentsWrite => ShipmentsWriteValue,
            _ => throw new ArgumentOutOfRangeException(
                nameof(scope), scope, "Unknown integration scope.")
        };

    internal static IntegrationScope ParseDatabaseValue(string value) =>
        value switch
        {
            ShipmentsReadValue => IntegrationScope.ShipmentsRead,
            ShipmentsWriteValue => IntegrationScope.ShipmentsWrite,
            _ => throw new InvalidOperationException("Unknown persisted integration scope.")
        };
}
