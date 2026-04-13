# Catalog Recovery Survivability - Sprint 17

Sprint 17 makes signer trust and archive proof easier to recover after host loss, restore, or migration.

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

## Archive Mirror Recovery

Set:

- `DossierArchiveBackend=filesystem_mirror`
- `EnableDossierArchiveMirror=true`
- `DossierArchiveRootPath`
- `DossierArchiveMirrorRootPath`

Archive writes are mirrored. Replay verification reads primary first and mirror second. A mirror recovery returns `RecoveryState=recovered_from_mirror`.

Recommended flow:

1. Archive the dossier.
2. Replay-verify the archive.
3. Confirm `archiveHash`, `dossierHash`, backend, storage URI, and recovery state.
4. After restore or migration, replay-verify again.
5. Treat any archive verification error as a release-audit incident.

## Disaster Review Checklist

- Active signer count is nonzero in production-like environments.
- Signer trust-store backup exists and verifies against the expected hash.
- Archive primary and mirror roots are different durability locations.
- Dossier archive replay verification passes after restore.
- Dossier review warnings are investigated before production-like completion.
