namespace Puntiro.IntegrationTests.Cloud;

public sealed record ApiProblemResponse(
    int Status,
    string Code,
    string Title,
    string TraceId);
