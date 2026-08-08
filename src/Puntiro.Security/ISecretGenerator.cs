namespace Puntiro.Security;

public interface ISecretGenerator
{
    void Fill(Span<byte> destination);
}
