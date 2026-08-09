namespace Puntiro.Provisioning.Cli;

internal static class ProvisioningOutcome
{
    internal static void Write(TextWriter writer, ProvisioningExit exit)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine(Message(exit));
    }

    internal static string Message(ProvisioningExit exit) => exit switch
    {
        ProvisioningExit.Success => "Provisioning completed.",
        ProvisioningExit.InvalidArguments => "Provisioning arguments are invalid.",
        ProvisioningExit.Conflict => "Provisioning stopped because durable state conflicts with the request.",
        ProvisioningExit.InvalidCredentials => "Provisioning credentials or confirmation are invalid.",
        _ => "Provisioning failed because infrastructure is unavailable."
    };
}
