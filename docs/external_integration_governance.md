# External Integration Governance - Sprint 9

External capabilities must not carry secret values as ordinary capability metadata. REST-backed capabilities use two layers:

1. `IntegrationBinding` on the capability descriptor chooses endpoint behavior, such as `connectionName`, `endpoint`, and `method`.
2. The platform catalog owns connection metadata, such as `BaseUrl`, retry, timeout, auth scheme, API key header, and secret references.

## Secret Rules

- Use `AuthTokenSecret`, `ApiKeySecret`, or `HeaderSecrets` in the external connection catalog.
- The REST adapter resolves those references through `ISecretProvider`.
- Raw `authToken` or `apiKey` metadata is rejected with `REST_SECRET_POLICY_VIOLATION`.
- Missing secret provider returns `REST_SECRET_PROVIDER_UNAVAILABLE`.
- Missing secret values return `REST_SECRET_NOT_FOUND`.

## Platform Catalog Shape

```json
{
  "PlatformCatalog": {
    "CatalogPath": "catalog/platform-catalog.json"
  },
  "ExternalConnections": {
    "Connections": {
      "external-stock-api": {
        "BaseUrl": "https://external-stock.example.com",
        "AuthScheme": "Bearer",
        "AuthTokenSecret": "tilsoft/external-stock-api/token",
        "TimeoutSeconds": 10,
        "RetryCount": 2,
        "RetryDelayMs": 100
      }
    }
  }
}
```

Capability binding:

```json
{
  "CapabilityKey": "warehouse.external-stock.lookup",
  "AdapterType": "rest-json",
  "IntegrationBinding": {
    "connectionName": "external-stock-api",
    "endpoint": "/warehouse/external-stock",
    "method": "GET"
  }
}
```

## Source Precedence

Capability source precedence is explicit:

1. Static fallback capability descriptors provide development/test defaults and stable keys.
2. Bootstrap configuration source overrides static descriptors by `CapabilityKey`.
3. Durable platform catalog source overrides both by `CapabilityKey`.

External connection precedence is:

1. Durable platform catalog records.
2. Bootstrap configuration records only when `PlatformCatalog:AllowBootstrapConfigurationFallback=true`.

Production endpoint/auth shape should live in platform catalog records with secret references, not static code literals or raw app configuration secrets.
