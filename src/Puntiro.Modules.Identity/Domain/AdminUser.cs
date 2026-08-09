namespace Puntiro.Modules.Identity.Domain;

public enum AdminUserStatus
{
    Provisioning,
    Active,
    Suspended
}

internal sealed class AdminUser
{
    private AdminUser()
    {
    }

    private AdminUser(
        Guid id,
        string displayEmail,
        string normalizedEmail,
        Guid provisioningOrganizationId,
        DateTimeOffset now)
    {
        EnsureId(id, nameof(id));
        EnsureId(provisioningOrganizationId, nameof(provisioningOrganizationId));
        Id = id;
        DisplayEmail = displayEmail;
        NormalizedEmail = normalizedEmail;
        ProvisioningOrganizationId = provisioningOrganizationId;
        Status = AdminUserStatus.Provisioning;
        CreatedAtUtc = EnsureUtc(now);
        UpdatedAtUtc = CreatedAtUtc;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public string DisplayEmail { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public AdminUserStatus Status { get; private set; }
    public Guid? ProvisioningOrganizationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    internal static AdminUser StartProvisioning(
        Guid id,
        string displayEmail,
        string normalizedEmail,
        Guid provisioningOrganizationId,
        DateTimeOffset now) =>
        new(id, displayEmail, normalizedEmail, provisioningOrganizationId, now);

    internal void ContinueProvisioning(string displayEmail, DateTimeOffset now)
    {
        if (Status != AdminUserStatus.Provisioning)
        {
            throw new InvalidOperationException("Only a provisioning account can be resumed.");
        }

        DisplayEmail = displayEmail;
        Touch(now);
    }

    internal void Activate(Guid organizationId, DateTimeOffset now)
    {
        EnsureId(organizationId, nameof(organizationId));
        if (Status != AdminUserStatus.Provisioning || ProvisioningOrganizationId != organizationId)
        {
            throw new InvalidOperationException("The owner account is not provisioning for this organization.");
        }

        Status = AdminUserStatus.Active;
        ProvisioningOrganizationId = null;
        Touch(now);
    }

    internal void Suspend(DateTimeOffset now)
    {
        if (Status != AdminUserStatus.Active)
        {
            throw new InvalidOperationException("Only an active account can be suspended.");
        }

        Status = AdminUserStatus.Suspended;
        Touch(now);
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAtUtc = EnsureUtc(now);
        Version++;
    }

    internal static DateTimeOffset EnsureUtc(DateTimeOffset value) =>
        value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();

    internal static void EnsureId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }
    }
}

internal sealed class PasswordCredential
{
    private PasswordCredential()
    {
    }

    internal PasswordCredential(
        Guid userId,
        Security.PasswordHash hash,
        DateTimeOffset now)
    {
        UserId = userId;
        Replace(hash, now, initial: true);
    }

    public Guid UserId { get; private set; }
    public byte[] Salt { get; private set; } = [];
    public byte[] Hash { get; private set; } = [];
    public int MemoryKiB { get; private set; }
    public int Iterations { get; private set; }
    public int Parallelism { get; private set; }
    public string Algorithm { get; private set; } = string.Empty;
    public DateTimeOffset SetAtUtc { get; private set; }
    public DateTimeOffset? LastRehashedAtUtc { get; private set; }
    public long Version { get; private set; }

    internal Security.PasswordHash Snapshot() =>
        new((byte[])Salt.Clone(), (byte[])Hash.Clone(), MemoryKiB, Iterations, Parallelism, Algorithm);

    internal void Replace(Security.PasswordHash hash, DateTimeOffset now, bool initial = false)
    {
        ArgumentNullException.ThrowIfNull(hash);
        Salt = (byte[])hash.Salt.Clone();
        Hash = (byte[])hash.Hash.Clone();
        MemoryKiB = hash.MemoryKiB;
        Iterations = hash.Iterations;
        Parallelism = hash.Parallelism;
        Algorithm = hash.Algorithm;
        SetAtUtc = AdminUser.EnsureUtc(now);
        LastRehashedAtUtc = initial ? null : SetAtUtc;
        Version = initial ? 1 : Version + 1;
    }
}

internal sealed class TotpCredential
{
    private TotpCredential()
    {
    }

    internal TotpCredential(Guid userId, byte[] protectedSecret, DateTimeOffset now)
    {
        UserId = userId;
        ProtectedSecret = (byte[])protectedSecret.Clone();
        ReplacedAtUtc = AdminUser.EnsureUtc(now);
        Version = 1;
    }

    public Guid UserId { get; private set; }
    public byte[] ProtectedSecret { get; private set; } = [];
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public long? LastAcceptedCounter { get; private set; }
    public DateTimeOffset ReplacedAtUtc { get; private set; }
    public long Version { get; private set; }

    internal void Accept(long counter, DateTimeOffset now, bool confirm)
    {
        LastAcceptedCounter = counter;
        if (confirm && ConfirmedAtUtc is null)
        {
            ConfirmedAtUtc = AdminUser.EnsureUtc(now);
        }

        Version++;
    }

    internal void Replace(byte[] protectedSecret, DateTimeOffset now)
    {
        ProtectedSecret = (byte[])protectedSecret.Clone();
        ConfirmedAtUtc = null;
        LastAcceptedCounter = null;
        ReplacedAtUtc = AdminUser.EnsureUtc(now);
        Version++;
    }
}

internal sealed class RecoveryCode
{
    private RecoveryCode()
    {
    }

    internal RecoveryCode(
        Guid id,
        Guid userId,
        Guid batchId,
        byte[] verifier,
        string keyVersion,
        DateTimeOffset issuedAtUtc)
    {
        Id = id;
        UserId = userId;
        BatchId = batchId;
        Verifier = (byte[])verifier.Clone();
        KeyVersion = keyVersion;
        IssuedAtUtc = AdminUser.EnsureUtc(issuedAtUtc);
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid BatchId { get; private set; }
    public byte[] Verifier { get; private set; } = [];
    public string KeyVersion { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public DateTimeOffset? UsedAtUtc { get; private set; }
    public long Version { get; private set; }

    internal void Use(DateTimeOffset now)
    {
        if (UsedAtUtc is not null)
        {
            throw new InvalidOperationException("Recovery code has already been used.");
        }

        UsedAtUtc = AdminUser.EnsureUtc(now);
        Version++;
    }
}

internal sealed class AdminSession
{
    private AdminSession()
    {
    }

    internal AdminSession(
        Guid id,
        Guid publicId,
        byte[] verifier,
        string keyVersion,
        Guid userId,
        Guid organizationId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset idleExpiresAtUtc,
        DateTimeOffset absoluteExpiresAtUtc,
        DateTimeOffset? secondFactorVerifiedAtUtc)
    {
        Id = id;
        PublicId = publicId;
        Verifier = (byte[])verifier.Clone();
        KeyVersion = keyVersion;
        UserId = userId;
        ActiveOrganizationId = organizationId;
        CreatedAtUtc = AdminUser.EnsureUtc(createdAtUtc);
        LastSeenAtUtc = CreatedAtUtc;
        IdleExpiresAtUtc = AdminUser.EnsureUtc(idleExpiresAtUtc);
        AbsoluteExpiresAtUtc = AdminUser.EnsureUtc(absoluteExpiresAtUtc);
        SecondFactorVerifiedAtUtc = secondFactorVerifiedAtUtc is null
            ? null
            : AdminUser.EnsureUtc(secondFactorVerifiedAtUtc.Value);
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid PublicId { get; private set; }
    public byte[] Verifier { get; private set; } = [];
    public string KeyVersion { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public Guid ActiveOrganizationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastSeenAtUtc { get; private set; }
    public DateTimeOffset IdleExpiresAtUtc { get; private set; }
    public DateTimeOffset AbsoluteExpiresAtUtc { get; private set; }
    public DateTimeOffset? SecondFactorVerifiedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? RevokeReason { get; private set; }
    public long Version { get; private set; }

    internal void Observe(DateTimeOffset now, DateTimeOffset idleExpiresAtUtc)
    {
        LastSeenAtUtc = AdminUser.EnsureUtc(now);
        IdleExpiresAtUtc = AdminUser.EnsureUtc(idleExpiresAtUtc);
        Version++;
    }

    internal void Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = AdminUser.EnsureUtc(now);
        RevokeReason = reason;
        Version++;
    }
}
