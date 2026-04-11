# appsettings Configuration Guide

## Connection String

The `Sql:ConnectionString` is intentionally empty in `appsettings.json`.

### How to configure:

**Option 1: Environment Variable (recommended for production)**
```
Sql__ConnectionString=Server=.;Database=TILSOFTAI;User ID=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;
```

**Option 2: User Secrets (recommended for development)**
```bash
dotnet user-secrets set "Sql:ConnectionString" "Server=.;Database=TILSOFTAI;User ID=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
```

**Option 3: Local Override File (gitignored)**
Create `appsettings.Local.json`:
```json
{
  "Sql": {
    "ConnectionString": "Server=.;Database=TILSOFTAI;User ID=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  }
}
```

> **Note:** Never commit passwords to source control.

## Capability Configuration

Runtime capabilities can be supplied in the `Capabilities` array. Configuration entries override static fallback capabilities by `CapabilityKey`.

```json
{
  "CapabilityKey": "warehouse.external-stock.lookup",
  "Domain": "warehouse",
  "AdapterType": "rest-json",
  "Operation": "execute_http_json",
  "TargetSystemId": "external-stock-api",
  "ExecutionMode": "readonly",
  "RequiredRoles": [ "warehouse_external_read" ],
  "AllowedTenants": [],
  "IntegrationBinding": {
    "baseUrl": "https://external-stock.example.com",
    "endpoint": "/warehouse/external-stock",
    "method": "GET",
    "timeoutSeconds": "10",
    "retryCount": "2",
    "retryDelayMs": "100",
    "authScheme": "Bearer",
    "authToken": "ENV_OR_SECRET_VALUE",
    "apiKeyHeader": "X-Api-Key",
    "apiKey": "ENV_OR_SECRET_VALUE"
  }
}
```

`RequiredRoles` and `AllowedTenants` are enforced before adapter resolution. REST adapter failures are classified as binding, client, server, timeout, transient HTTP, or transport failures.
