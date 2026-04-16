# Signed Artifact Verification Decision

Sprint 24 decision: defer full publisher-signature verification.

## Decision

The platform already supports strong evidence integrity through artifact hashes, trusted artifact roots, provider verification, signer trust-store lifecycle work, promotion manifests, rollout attestations, and dossier hashes. Sprint 24 does not add a new publisher-signature verification implementation.

## Rationale

- The current priority is executable operational proof: certification evidence collection, release evidence bundles, fallback posture capture, and generated readiness packets.
- No current sprint input requires publisher-signature proof as a blocking compliance control.
- Adding a new signature-verification path without a concrete compliance requirement would widen scope and distract from live certification execution.

## Scope Reserved For A Future Sprint

If compliance requires `signature_verified`, a future sprint should add:

- accepted signing algorithms and key custody requirements,
- signature metadata on evidence artifacts,
- verifier integration in the evidence trust flow,
- negative tests for unsigned, expired, revoked, or mismatched signatures,
- release-gate policy that can require `signature_verified` for selected environments.

Until then, release evidence bundles must include SHA-256 hashes and approved evidence URIs, and production-like gates should continue to require the configured trust tier.
