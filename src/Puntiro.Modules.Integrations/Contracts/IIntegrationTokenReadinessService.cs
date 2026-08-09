namespace Puntiro.Modules.Integrations.Contracts;

/// <summary>
/// Performs a read-only retained-key check for durable, unrevoked integration credentials.
/// </summary>
public interface IIntegrationTokenReadinessService
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}
