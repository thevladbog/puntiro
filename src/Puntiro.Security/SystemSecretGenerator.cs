using System.Security.Cryptography;

namespace Puntiro.Security;

public sealed class SystemSecretGenerator : ISecretGenerator
{
    public void Fill(Span<byte> destination)
    {
        RandomNumberGenerator.Fill(destination);
    }
}
