using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class FileSystemPlatformCatalogSignerTrustStore : IPlatformCatalogSignerTrustStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly CatalogCertificationOptions _options;
    private readonly object _gate = new();

    public FileSystemPlatformCatalogSignerTrustStore(IOptions<CatalogCertificationOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public IReadOnlyList<CatalogTrustedSignerRecord> ListSigners()
    {
        lock (_gate)
        {
            var envelope = Load();
            var governed = envelope.Signers
                .Select(item => item with { Source = "governed_file" })
                .ToArray();
            var governedKeys = governed
                .Select(item => Key(item.SignerId, item.KeyId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var configured = _options.TrustedEvidenceSigners
                .Where(item => !governedKeys.Contains(Key(item.SignerId, item.KeyId)))
                .Select(ToRecord)
                .ToArray();

            return governed.Concat(configured)
                .OrderBy(item => item.SignerId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.KeyId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public CatalogTrustedSignerRecord? FindSigner(string signerId, string keyId) =>
        ListSigners().FirstOrDefault(item =>
            string.Equals(item.SignerId, signerId, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(keyId)
                || string.Equals(item.KeyId, keyId, StringComparison.OrdinalIgnoreCase)));

    public IReadOnlyList<CatalogSignerTrustChangeRecord> ListChanges()
    {
        lock (_gate)
        {
            return Load().Changes
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToArray();
        }
    }

    public CatalogSignerTrustMutationResult ProposeChange(
        CatalogSignerTrustMutationRequest request,
        CatalogMutationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var blockers = ValidateRequest(request);
        if (blockers.Count > 0)
        {
            return Blocked(blockers);
        }

        var now = DateTime.UtcNow;
        var change = new CatalogSignerTrustChangeRecord
        {
            ChangeId = Guid.NewGuid().ToString("N"),
            Operation = NormalizeOperation(request.Operation),
            Status = CatalogSignerTrustChangeStatus.Proposed,
            SignerId = request.SignerId.Trim(),
            KeyId = request.KeyId.Trim(),
            PublicKeyPem = request.PublicKeyPem.Trim(),
            PublicKeyFingerprint = string.IsNullOrWhiteSpace(request.PublicKeyPem)
                ? string.Empty
                : Fingerprint(request.PublicKeyPem),
            RotatesFromKeyId = request.RotatesFromKeyId.Trim(),
            ValidFromUtc = request.ValidFromUtc,
            ValidUntilUtc = request.ValidUntilUtc,
            Reason = request.Reason.Trim(),
            PolicyVersion = _options.PolicyVersion,
            RequestedByUserId = context.UserId,
            CorrelationId = context.CorrelationId,
            CreatedAtUtc = now
        };

        lock (_gate)
        {
            var envelope = Load();
            envelope.Changes.Add(change);
            Save(envelope);
        }

        return new CatalogSignerTrustMutationResult
        {
            IsAccepted = true,
            Change = change
        };
    }

    public CatalogSignerTrustMutationResult ApproveChange(
        string changeId,
        CatalogMutationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        lock (_gate)
        {
            var envelope = Load();
            var change = FindChange(envelope, changeId);
            if (change is null)
            {
                return Blocked(new[] { "signer_trust_change_not_found" });
            }

            if (!string.Equals(change.Status, CatalogSignerTrustChangeStatus.Proposed, StringComparison.OrdinalIgnoreCase))
            {
                return Blocked(new[] { $"signer_trust_change_not_proposed:{change.Status}" });
            }

            if (_options.RequireIndependentSignerTrustApproval
                && string.Equals(change.RequestedByUserId, context.UserId, StringComparison.OrdinalIgnoreCase))
            {
                return Blocked(new[] { "signer_trust_independent_approval_required" });
            }

            var approved = change with
            {
                Status = CatalogSignerTrustChangeStatus.Approved,
                ApprovedByUserId = context.UserId,
                ApprovedAtUtc = DateTime.UtcNow
            };
            ReplaceChange(envelope, approved);
            Save(envelope);
            return new CatalogSignerTrustMutationResult { IsAccepted = true, Change = approved };
        }
    }

    public CatalogSignerTrustMutationResult RejectChange(
        string changeId,
        CatalogMutationContext context,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(context);

        lock (_gate)
        {
            var envelope = Load();
            var change = FindChange(envelope, changeId);
            if (change is null)
            {
                return Blocked(new[] { "signer_trust_change_not_found" });
            }

            if (!string.Equals(change.Status, CatalogSignerTrustChangeStatus.Proposed, StringComparison.OrdinalIgnoreCase))
            {
                return Blocked(new[] { $"signer_trust_change_not_proposed:{change.Status}" });
            }

            var rejected = change with
            {
                Status = CatalogSignerTrustChangeStatus.Rejected,
                RejectedByUserId = context.UserId,
                RejectedAtUtc = DateTime.UtcNow,
                Reason = string.IsNullOrWhiteSpace(reason) ? change.Reason : $"{change.Reason}; rejection: {reason.Trim()}"
            };
            ReplaceChange(envelope, rejected);
            Save(envelope);
            return new CatalogSignerTrustMutationResult { IsAccepted = true, Change = rejected };
        }
    }

    public CatalogSignerTrustMutationResult ApplyChange(
        string changeId,
        CatalogMutationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        lock (_gate)
        {
            var envelope = Load();
            var change = FindChange(envelope, changeId);
            if (change is null)
            {
                return Blocked(new[] { "signer_trust_change_not_found" });
            }

            if (!string.Equals(change.Status, CatalogSignerTrustChangeStatus.Approved, StringComparison.OrdinalIgnoreCase))
            {
                return Blocked(new[] { $"signer_trust_change_not_approved:{change.Status}" });
            }

            var blockers = ApplyToSigners(envelope, change, context);
            if (blockers.Count > 0)
            {
                return Blocked(blockers);
            }

            var version = NextVersion(envelope);
            var applied = change with
            {
                Status = CatalogSignerTrustChangeStatus.Applied,
                AppliedByUserId = context.UserId,
                AppliedAtUtc = DateTime.UtcNow,
                ResultingTrustStoreVersion = version
            };
            envelope.TrustStoreVersion = version;
            ReplaceChange(envelope, applied);
            Save(envelope);
            return new CatalogSignerTrustMutationResult { IsAccepted = true, Change = applied };
        }
    }

    public CatalogSignerTrustStoreRecoveryResult BackupTrustStore()
    {
        lock (_gate)
        {
            var envelope = Load();
            var source = StorePath();
            var backup = BackupPath();
            var content = Serialize(envelope);
            var directory = Path.GetDirectoryName(backup);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(backup, content);
            return RecoveryResult("backup", source, backup, envelope, Hash(content), string.Empty, Array.Empty<string>());
        }
    }

    public CatalogSignerTrustStoreRecoveryResult VerifyTrustStoreBackup(string expectedTrustStoreHash)
    {
        lock (_gate)
        {
            var source = StorePath();
            var backup = BackupPath();
            if (!File.Exists(backup))
            {
                return RecoveryResult("verify_backup", source, backup, new CatalogSignerTrustStoreEnvelope(), string.Empty, expectedTrustStoreHash, new[] { "trust_store_backup_not_found" });
            }

            var content = File.ReadAllText(backup);
            var envelope = Deserialize(content);
            var hash = Hash(content);
            var errors = new List<string>();
            if (!string.IsNullOrWhiteSpace(expectedTrustStoreHash)
                && !string.Equals(hash, expectedTrustStoreHash, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("trust_store_backup_hash_mismatch");
            }

            return RecoveryResult("verify_backup", source, backup, envelope, hash, expectedTrustStoreHash, errors);
        }
    }

    public CatalogSignerTrustStoreRecoveryResult RestoreTrustStoreBackup(string expectedTrustStoreHash)
    {
        lock (_gate)
        {
            var verification = VerifyTrustStoreBackup(expectedTrustStoreHash);
            if (!verification.IsVerified)
            {
                return verification with { Operation = "restore_backup" };
            }

            var source = StorePath();
            var backup = BackupPath();
            var directory = Path.GetDirectoryName(source);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(backup, source, overwrite: true);
            var content = File.ReadAllText(source);
            return RecoveryResult("restore_backup", source, backup, Deserialize(content), Hash(content), expectedTrustStoreHash, Array.Empty<string>());
        }
    }

    private List<string> ApplyToSigners(
        CatalogSignerTrustStoreEnvelope envelope,
        CatalogSignerTrustChangeRecord change,
        CatalogMutationContext context)
    {
        var blockers = new List<string>();
        var now = DateTime.UtcNow;
        var operation = NormalizeOperation(change.Operation);
        var current = ListSigners().FirstOrDefault(item =>
            string.Equals(item.SignerId, change.SignerId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.KeyId, change.KeyId, StringComparison.OrdinalIgnoreCase));

        if (operation is CatalogSignerTrustChangeOperations.AddSignerKey && current is not null)
        {
            blockers.Add("signer_key_already_exists");
        }

        if (operation is CatalogSignerTrustChangeOperations.RotateSignerKey)
        {
            var previous = ListSigners().FirstOrDefault(item =>
                string.Equals(item.SignerId, change.SignerId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.KeyId, change.RotatesFromKeyId, StringComparison.OrdinalIgnoreCase));
            if (previous is null)
            {
                blockers.Add("signer_rotation_source_key_not_found");
            }
            else
            {
                UpsertSigner(envelope, previous with
                {
                    Status = CatalogSignerLifecycleStates.Rotated,
                    ValidUntilUtc = now,
                    RotatedToKeyId = change.KeyId,
                    TrustStoreVersion = NextVersion(envelope),
                    ApprovedChangeId = change.ChangeId,
                    LastChangedAtUtc = now,
                    Source = "governed_file"
                });
            }
        }

        if (operation is CatalogSignerTrustChangeOperations.RevokeSignerKey or CatalogSignerTrustChangeOperations.RetireSignerKey)
        {
            if (current is null)
            {
                blockers.Add("signer_key_not_found");
            }
            else
            {
                UpsertSigner(envelope, current with
                {
                    Status = operation is CatalogSignerTrustChangeOperations.RevokeSignerKey
                        ? CatalogSignerLifecycleStates.Revoked
                        : CatalogSignerLifecycleStates.Retired,
                    RevokedAtUtc = operation is CatalogSignerTrustChangeOperations.RevokeSignerKey ? now : current.RevokedAtUtc,
                    ValidUntilUtc = current.ValidUntilUtc ?? now,
                    TrustStoreVersion = NextVersion(envelope),
                    ApprovedChangeId = change.ChangeId,
                    LastChangedAtUtc = now,
                    Source = "governed_file"
                });
            }
        }

        if (operation is CatalogSignerTrustChangeOperations.AddSignerKey or CatalogSignerTrustChangeOperations.RotateSignerKey)
        {
            UpsertSigner(envelope, new CatalogTrustedSignerRecord
            {
                SignerId = change.SignerId,
                KeyId = change.KeyId,
                PublicKeyPem = change.PublicKeyPem,
                PublicKeyFingerprint = change.PublicKeyFingerprint,
                Status = CatalogSignerLifecycleStates.Active,
                ValidFromUtc = change.ValidFromUtc ?? now,
                ValidUntilUtc = change.ValidUntilUtc,
                TrustStoreVersion = NextVersion(envelope),
                ApprovedChangeId = change.ChangeId,
                LastChangedAtUtc = now,
                Source = "governed_file"
            });
        }

        return blockers;
    }

    private List<string> ValidateRequest(CatalogSignerTrustMutationRequest request)
    {
        var blockers = new List<string>();
        var operation = NormalizeOperation(request.Operation);
        if (!KnownOperation(operation))
        {
            blockers.Add("signer_trust_operation_invalid");
        }

        if (string.IsNullOrWhiteSpace(request.SignerId))
        {
            blockers.Add("signer_id_required");
        }

        if (string.IsNullOrWhiteSpace(request.KeyId))
        {
            blockers.Add("signer_key_id_required");
        }

        if (operation is CatalogSignerTrustChangeOperations.AddSignerKey or CatalogSignerTrustChangeOperations.RotateSignerKey)
        {
            if (string.IsNullOrWhiteSpace(request.PublicKeyPem))
            {
                blockers.Add("signer_public_key_required");
            }
            else if (!IsValidPublicKey(request.PublicKeyPem))
            {
                blockers.Add("signer_public_key_invalid");
            }
        }

        if (operation is CatalogSignerTrustChangeOperations.RotateSignerKey
            && string.IsNullOrWhiteSpace(request.RotatesFromKeyId))
        {
            blockers.Add("signer_rotation_source_key_required");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            blockers.Add("signer_trust_change_reason_required");
        }

        return blockers;
    }

    private CatalogSignerTrustStoreEnvelope Load()
    {
        var path = StorePath();
        if (!File.Exists(path))
        {
            return new CatalogSignerTrustStoreEnvelope();
        }

        return Deserialize(File.ReadAllText(path));
    }

    private void Save(CatalogSignerTrustStoreEnvelope envelope)
    {
        var path = StorePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, Serialize(envelope));
    }

    private string StorePath() => Path.GetFullPath(_options.SignerTrustStorePath);

    private string BackupPath() => Path.GetFullPath(_options.SignerTrustStoreBackupPath);

    private static CatalogSignerTrustStoreEnvelope Deserialize(string json) =>
        JsonSerializer.Deserialize<CatalogSignerTrustStoreEnvelope>(json, JsonOptions)
        ?? new CatalogSignerTrustStoreEnvelope();

    private static string Serialize(CatalogSignerTrustStoreEnvelope envelope) =>
        JsonSerializer.Serialize(envelope, JsonOptions);

    private static CatalogSignerTrustStoreRecoveryResult RecoveryResult(
        string operation,
        string source,
        string backup,
        CatalogSignerTrustStoreEnvelope envelope,
        string hash,
        string expectedHash,
        IReadOnlyList<string> errors) => new()
        {
            IsVerified = errors.Count == 0,
            Operation = operation,
            SourcePath = source,
            BackupPath = backup,
            TrustStoreHash = hash,
            ExpectedTrustStoreHash = expectedHash,
            TrustStoreVersion = envelope.TrustStoreVersion,
            SignerCount = envelope.Signers.Count,
            ChangeCount = envelope.Changes.Count,
            VerifiedAtUtc = DateTime.UtcNow,
            Errors = errors
        };

    private static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static CatalogTrustedSignerRecord ToRecord(CatalogTrustedSignerOptions options) => new()
    {
        SignerId = options.SignerId,
        KeyId = options.KeyId,
        PublicKeyPem = options.PublicKeyPem,
        PublicKeyFingerprint = Fingerprint(options.PublicKeyPem),
        Status = string.IsNullOrWhiteSpace(options.Status) ? CatalogSignerLifecycleStates.Active : options.Status.Trim().ToLowerInvariant(),
        ValidFromUtc = options.ValidFromUtc,
        ValidUntilUtc = options.ValidUntilUtc,
        RevokedAtUtc = options.RevokedAtUtc,
        RotatedToKeyId = options.RotatedToKeyId,
        TrustStoreVersion = string.IsNullOrWhiteSpace(options.TrustStoreVersion) ? "config" : options.TrustStoreVersion,
        ApprovedChangeId = string.IsNullOrWhiteSpace(options.ApprovedChangeId) ? "config" : options.ApprovedChangeId,
        LastChangedAtUtc = options.LastChangedAtUtc,
        Source = "static_config"
    };

    private static void UpsertSigner(CatalogSignerTrustStoreEnvelope envelope, CatalogTrustedSignerRecord signer)
    {
        envelope.Signers.RemoveAll(item =>
            string.Equals(item.SignerId, signer.SignerId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.KeyId, signer.KeyId, StringComparison.OrdinalIgnoreCase));
        envelope.Signers.Add(signer);
    }

    private static CatalogSignerTrustChangeRecord? FindChange(CatalogSignerTrustStoreEnvelope envelope, string changeId) =>
        envelope.Changes.FirstOrDefault(item => string.Equals(item.ChangeId, changeId, StringComparison.OrdinalIgnoreCase));

    private static void ReplaceChange(CatalogSignerTrustStoreEnvelope envelope, CatalogSignerTrustChangeRecord change)
    {
        envelope.Changes.RemoveAll(item => string.Equals(item.ChangeId, change.ChangeId, StringComparison.OrdinalIgnoreCase));
        envelope.Changes.Add(change);
    }

    private static string NormalizeOperation(string operation) => operation.Trim().ToLowerInvariant();

    private static bool KnownOperation(string operation) =>
        operation is CatalogSignerTrustChangeOperations.AddSignerKey
            or CatalogSignerTrustChangeOperations.RotateSignerKey
            or CatalogSignerTrustChangeOperations.RevokeSignerKey
            or CatalogSignerTrustChangeOperations.RetireSignerKey;

    private static string NextVersion(CatalogSignerTrustStoreEnvelope envelope) =>
        $"trust-store-{envelope.Changes.Count(item => string.Equals(item.Status, CatalogSignerTrustChangeStatus.Applied, StringComparison.OrdinalIgnoreCase)) + 1}";

    private static string Key(string signerId, string keyId) => $"{signerId.Trim()}::{keyId.Trim()}";

    private static CatalogSignerTrustMutationResult Blocked(IReadOnlyList<string> blockers) => new()
    {
        IsAccepted = false,
        Blockers = blockers
    };

    private static bool IsValidPublicKey(string publicKeyPem)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static string Fingerprint(string publicKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        return Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
    }

    private sealed class CatalogSignerTrustStoreEnvelope
    {
        public string TrustStoreVersion { get; set; } = "trust-store-0";
        public List<CatalogTrustedSignerRecord> Signers { get; set; } = new();
        public List<CatalogSignerTrustChangeRecord> Changes { get; set; } = new();
    }
}
