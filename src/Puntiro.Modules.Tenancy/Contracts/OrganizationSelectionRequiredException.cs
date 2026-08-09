namespace Puntiro.Modules.Tenancy.Contracts;

public sealed class OrganizationSelectionRequiredException : InvalidOperationException
{
    public OrganizationSelectionRequiredException()
        : base("More than one active organization membership requires an explicit selection.")
    {
    }
}
