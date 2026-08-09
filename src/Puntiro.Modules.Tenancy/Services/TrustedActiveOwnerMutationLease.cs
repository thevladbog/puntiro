using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore.Storage;
using Puntiro.Modules.Tenancy.Contracts;

namespace Puntiro.Modules.Tenancy.Services;

internal interface ITrustedMutationTransaction : IAsyncDisposable
{
    Task RollbackAsync(CancellationToken cancellationToken);
}

internal sealed class EfTrustedMutationTransaction(
    IDbContextTransaction transaction) : ITrustedMutationTransaction
{
    public Task RollbackAsync(CancellationToken cancellationToken) =>
        transaction.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}

internal sealed class TrustedActiveOwnerMutationLease(
    ITrustedMutationTransaction transaction) : ITrustedActiveOwnerMutationLease
{
    private ITrustedMutationTransaction? _transaction = transaction;

    public async ValueTask DisposeAsync()
    {
        var current = Interlocked.Exchange(ref _transaction, null);
        if (current is null)
        {
            return;
        }

        Exception? rollbackError = null;
        Exception? disposeError = null;
        try
        {
            try
            {
                await current.RollbackAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                rollbackError = exception;
            }
        }
        finally
        {
            try
            {
                await current.DisposeAsync();
            }
            catch (Exception exception)
            {
                disposeError = exception;
            }
        }

        if (rollbackError is not null && disposeError is not null)
        {
            throw new AggregateException(
                "The trusted mutation transaction could not be cleaned up.",
                rollbackError,
                disposeError);
        }

        if (rollbackError is not null)
        {
            ExceptionDispatchInfo.Capture(rollbackError).Throw();
        }

        if (disposeError is not null)
        {
            ExceptionDispatchInfo.Capture(disposeError).Throw();
        }
    }
}
