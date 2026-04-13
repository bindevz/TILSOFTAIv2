# Catalog Recovery Survivability - Sprint 18

Sprint 18 moves signer trust recovery and archive replay from local survivability toward a stronger custody boundary. Filesystem and filesystem mirror storage remain valid, but production-like policy can now require managed durable backends.

## Signer Trust Recovery

Use:

- `POST /api/platform-catalog/signer-trust/recovery/backup`
- `POST /api/platform-catalog/signer-trust/recovery/verify-backup`
- `POST /api/platform-catalog/signer-trust/recovery/restore-backup`

Recommended flow:

1. Apply governed signer trust changes.
2. Run trust-store backup.
3. Store the returned `trustStoreHash` with the change ticket.
4. Verify backup with that expected hash before migration.
5. Restore with that expected hash after host replacement.
6. List signers and changes to confirm lifecycle continuity.

Machine-readable failures:

- `trust_store_backup_not_found`
- `trust_store_backup_hash_mismatch`

Recovery results include the backup backend name, backend durability class, and custody boundary. Current classes:

- `local_filesystem`: file-backed recovery on the local host family.
- `managed_durable`: managed SQL recovery storage outside the signer trust-store file path.

Production-like defaults require `MinimumTrustStoreDurabilityClassForProductionLike=managed_durable`.

## Archive Backends

Supported archive backends:

- `filesystem`: primary local archive path, durability class `local_filesystem`, retention posture `metadata_only`, immutability not enforced by backend.
- `filesystem_mirror`: primary plus mirror archive path, durability class `mirrored_filesystem`, retention posture `metadata_only`, immutability not enforced by backend.
- `managed_sql`: SQL-backed archive storage, durability class `managed_durable`, retention posture `retention_tracked`, immutability enforced by stored procedure hash checks.

Recommended production-like settings:

- `DossierArchiveBackend=managed_sql`
- `MinimumArchiveDurabilityClassForProductionLike=managed_durable`
- `RequiredArchiveRetentionPostureForProductionLike=retention_tracked`

## Archive Mirror Recovery

Set:

- `DossierArchiveBackend=filesystem_mirror`
- `EnableDossierArchiveMirror=true`
- `DossierArchiveRootPath`
- `DossierArchiveMirrorRootPath`

Archive writes are mirrored. Replay verification reads primary first and mirror second. A mirror recovery returns `RecoveryState=recovered_from_mirror`.

Mirror recovery is useful for local survivability drills, but it does not satisfy the default Sprint 18 managed durability policy for production-like completion.

## Managed SQL Archive Recovery

Set:

- `DossierArchiveBackend=managed_sql`

Archive writes go through `dbo.app_platform_archive_upsert`. The procedure rejects a second write for the same manifest when the archive hash differs. Replay verification reads with `dbo.app_platform_archive_get` and reports `RecoveryState=managed_sql_read`.

Recommended flow:

1. Archive the dossier.
2. Replay-verify the archive.
3. Confirm `archiveHash`, `dossierHash`, backend, storage URI, and recovery state.
4. After restore or migration, replay-verify again.
5. Confirm backend class, retention posture, and immutability fields match policy.
6. Treat any archive verification error as a release-audit incident.

## Backend Policy

Production-like rollout completion checks archive replay verification when archived dossiers are required. Completion blocks with:

- `dossier_archive_required`
- `dossier_archive_verification_failed:{error}`
- `dossier_archive_policy_failure:archive_durability_class_insufficient:{actual}:{required}`
- `dossier_archive_policy_failure:archive_retention_posture_insufficient:{actual}:{required}`

Dossier review emits matching `dossier_archive_policy_warning:{failure}` warnings for operators before completion.

## Disaster Review Checklist

- Active signer count is nonzero in production-like environments.
- Signer trust-store backup exists and verifies against the expected hash.
- Signer trust-store backup class meets production-like policy.
- Archive backend class meets production-like policy.
- Archive retention posture meets production-like policy.
- Archive immutability is claimed only by a backend that enforces it.
- Archive primary and mirror roots are different durability locations when filesystem mirror mode is used.
- Dossier archive replay verification passes after restore.
- Dossier review warnings are investigated before production-like completion.
