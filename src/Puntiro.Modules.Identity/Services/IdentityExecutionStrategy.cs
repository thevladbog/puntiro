using System.Data;
using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Identity.Persistence;

namespace Puntiro.Modules.Identity.Services;

internal static class IdentityExecutionStrategy
{
    internal static Task<TResult> ExecuteInTransactionAsync<TResult>(
        IdentityDbContext context,
        Func<CancellationToken, Task<TResult>> operation,
        Func<CancellationToken, Task<bool>>? verifySucceeded,
        CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return ExecutionStrategyExtensions.ExecuteInTransactionAsync<ExecutionState<TResult>, TResult>(
            strategy,
            new ExecutionState<TResult>(context, operation, verifySucceeded),
            static async (state, token) =>
            {
                state.Context.ChangeTracker.Clear();
                return await state.Operation(token);
            },
            static async (state, token) =>
            {
                state.Context.ChangeTracker.Clear();
                return state.VerifySucceeded is not null && await state.VerifySucceeded(token);
            },
            static (dbContext, token) => dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                token),
            cancellationToken);
    }

    private sealed record ExecutionState<TResult>(
        IdentityDbContext Context,
        Func<CancellationToken, Task<TResult>> Operation,
        Func<CancellationToken, Task<bool>>? VerifySucceeded);
}
