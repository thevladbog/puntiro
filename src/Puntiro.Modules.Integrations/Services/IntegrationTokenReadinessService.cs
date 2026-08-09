using Microsoft.EntityFrameworkCore;
using Puntiro.Modules.Integrations.Contracts;
using Puntiro.Modules.Integrations.Persistence;
using Puntiro.Modules.Integrations.Security;

namespace Puntiro.Modules.Integrations.Services;

internal sealed class IntegrationTokenReadinessService(
    IntegrationsDbContext context,
    IntegrationKeyOptions keyOptions) : IIntegrationTokenReadinessService
{
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        var retainedVersions = await context.IntegrationTokens
            .AsNoTracking()
            .Where(item => item.RevokedAtUtc == null)
            .Select(item => item.KeyVersion)
            .Distinct()
            .ToListAsync(cancellationToken);
        return retainedVersions.All(keyOptions.HasKey);
    }
}
