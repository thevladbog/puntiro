using Puntiro.Modules.Tenancy.Services;
using Xunit;

namespace Puntiro.UnitTests.Tenancy;

public sealed class TrustedActiveOwnerMutationLeaseTests
{
    [Fact]
    public async Task Rollback_failure_still_disposes_once_and_second_disposal_is_idempotent()
    {
        var transaction = new FaultingTrustedMutationTransaction(
            rollbackException: new InvalidOperationException("rollback"));
        var lease = new TrustedActiveOwnerMutationLease(transaction);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await lease.DisposeAsync());
        await lease.DisposeAsync();

        Assert.Equal("rollback", error.Message);
        Assert.Equal(1, transaction.RollbackCalls);
        Assert.Equal(1, transaction.DisposeCalls);
    }

    [Fact]
    public async Task Rollback_and_disposal_failures_are_both_preserved()
    {
        var transaction = new FaultingTrustedMutationTransaction(
            new InvalidOperationException("rollback"),
            new IOException("dispose"));
        var lease = new TrustedActiveOwnerMutationLease(transaction);

        var error = await Assert.ThrowsAsync<AggregateException>(
            async () => await lease.DisposeAsync());

        Assert.Collection(
            error.InnerExceptions,
            item => Assert.Equal("rollback", item.Message),
            item => Assert.Equal("dispose", item.Message));
        Assert.Equal(1, transaction.RollbackCalls);
        Assert.Equal(1, transaction.DisposeCalls);
    }

    [Fact]
    public async Task Disposal_failure_is_preserved_after_successful_rollback()
    {
        var transaction = new FaultingTrustedMutationTransaction(
            disposeException: new IOException("dispose"));
        var lease = new TrustedActiveOwnerMutationLease(transaction);

        var error = await Assert.ThrowsAsync<IOException>(
            async () => await lease.DisposeAsync());
        await lease.DisposeAsync();

        Assert.Equal("dispose", error.Message);
        Assert.Equal(1, transaction.RollbackCalls);
        Assert.Equal(1, transaction.DisposeCalls);
    }
}

internal sealed class FaultingTrustedMutationTransaction(
    Exception? rollbackException = null,
    Exception? disposeException = null) : ITrustedMutationTransaction
{
    internal int RollbackCalls { get; private set; }
    internal int DisposeCalls { get; private set; }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        RollbackCalls++;
        return rollbackException is null
            ? Task.CompletedTask
            : Task.FromException(rollbackException);
    }

    public ValueTask DisposeAsync()
    {
        DisposeCalls++;
        return disposeException is null
            ? ValueTask.CompletedTask
            : ValueTask.FromException(disposeException);
    }
}
