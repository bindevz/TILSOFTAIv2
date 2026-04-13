# Catalog Promotion Manifest Provenance - Sprint 13

Promotion manifests are immutable release records that bind together the reviewed change, trusted evidence, gate result, environment, actor, and manifest hash.

## Manifest Issuance

Use `POST /api/platform-catalog/promotion-manifests` with:

- target `environmentName`,
- reviewed `changeIds`,
- trusted `evidenceIds`,
- optional `rollbackOfManifestId`,
- optional `relatedIncidentId`,
- operator notes.

The platform issues a manifest only when:

- all listed evidence exists in the target environment,
- required production-like evidence kinds are covered by trusted evidence,
- every change passes promotion gate evaluation,
- source mode and expected-version blockers are absent.

The manifest hash is computed from immutable manifest fields. Rollout state is recorded separately as append-only attestations.

## Rollout Attestation

Use `POST /api/platform-catalog/promotion-manifests/{manifestId}/attestations` to append rollout events:

- `started`,
- `completed`,
- `failed`,
- `aborted`,
- `superseded`.

Production-like completion requires trusted attestation evidence by default.

## Rollback And Emergency Lineage

Rollback manifests must set `rollbackOfManifestId` and reference the compensating catalog change. Emergency manifests should also carry `relatedIncidentId` and after-action evidence IDs.
