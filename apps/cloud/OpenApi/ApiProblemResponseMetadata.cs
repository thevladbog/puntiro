namespace Puntiro.Cloud.OpenApi;

public sealed class ApiProblemResponseMetadata
{
    public ApiProblemResponseMetadata(int statusCode, params string[] codes)
    {
        if (statusCode is < 400 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode));
        }

        if (codes.Length == 0 || codes.Any(string.IsNullOrWhiteSpace) ||
            codes.Distinct(StringComparer.Ordinal).Count() != codes.Length)
        {
            throw new ArgumentException("Problem codes must be non-empty and unique.", nameof(codes));
        }

        StatusCode = statusCode;
        Codes = codes;
    }

    public int StatusCode { get; }
    public IReadOnlyList<string> Codes { get; }
}
