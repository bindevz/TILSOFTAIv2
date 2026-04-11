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
    "connectionName": "external-stock-api",
    "endpoint": "/warehouse/external-stock",
    "method": "GET"
  },
  "ArgumentContract": {
    "RequiredArguments": [ "@ItemNo" ],
    "AllowedArguments": [ "@ItemNo" ],
    "AllowAdditionalArguments": false
  }
}
```

`RequiredRoles`, `AllowedTenants`, and `ArgumentContract` are enforced before adapter resolution.

External auth belongs in the connection catalog, not raw capability metadata:

```json
{
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

The REST adapter resolves secret references through `ISecretProvider` and rejects raw `authToken` or `apiKey` metadata.
