using Puntiro.Modules.Identity.Security;

namespace Puntiro.Modules.Identity.Contracts;

public static class AdminEmailPartition
{
    public static string Normalize(string input)
    {
        try
        {
            return EmailAddress.Normalize(input).Normalized;
        }
        catch (ArgumentException)
        {
            return "<invalid>";
        }
    }
}
