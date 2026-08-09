using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Puntiro.Modules.Integrations.Persistence;

internal interface IIntegrationTokenTransactionFactory
{
    Task<IIntegrationTokenTransaction> BeginSerializableAsync(
        CancellationToken cancellationToken);
}

internal interface IIntegrationTokenTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

internal sealed class EfIntegrationTokenTransactionFactory(
    IntegrationsDbContext context) : IIntegrationTokenTransactionFactory
{
    public async Task<IIntegrationTokenTransaction> BeginSerializableAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfIntegrationTokenTransaction(transaction);
    }

    private sealed class EfIntegrationTokenTransaction(
        IDbContextTransaction transaction) : IIntegrationTokenTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
