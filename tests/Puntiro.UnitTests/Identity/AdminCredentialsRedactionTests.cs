using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Puntiro.Modules.Identity.Contracts;
using Xunit;

namespace Puntiro.UnitTests.Identity;

public sealed class AdminCredentialsRedactionTests
{
    [Fact]
    public void Application_credentials_never_expose_fields_to_json_string_or_debugger_views()
    {
        var credentials = new AdminCredentials(
            "email-marker@example.test",
            "password-marker",
            "totp-marker",
            "recovery-marker");

        var json = JsonSerializer.Serialize(credentials);
        Assert.Equal("{}", json);
        Assert.Equal(nameof(AdminCredentials), credentials.ToString());
        Assert.DoesNotContain("marker", credentials.ToString(), StringComparison.Ordinal);

        var debuggerDisplay = typeof(AdminCredentials).GetCustomAttribute<DebuggerDisplayAttribute>();
        Assert.NotNull(debuggerDisplay);
        Assert.DoesNotContain("marker", debuggerDisplay.Value, StringComparison.Ordinal);
        Assert.All(
            typeof(AdminCredentials).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => Assert.Equal(
                DebuggerBrowsableState.Never,
                property.GetCustomAttribute<DebuggerBrowsableAttribute>()?.State));
    }
}
