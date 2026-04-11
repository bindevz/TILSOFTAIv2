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

## Platform Catalog Configuration

Production capability and external connection records are loaded from the platform catalog, not primarily from `appsettings.json`.

```json
{
  "PlatformCatalog": {
    "Enabled": true,
    "CatalogPath": "catalog/platform-catalog.json",
    "AllowBootstrapConfigurationFallback": true
  },
  "Capabilities": [],
  "ExternalConnections": {
    "Connections": {}
  }
}
```

Source precedence is:

1. Static fallback capabilities.
2. Bootstrap app configuration, when present.
3. Durable platform catalog records from `PlatformCatalog:CatalogPath`.

Catalog capability records use the same shape as bootstrap capability records:

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
    "AllowAdditionalArguments": false,
    "Arguments": [
      {
        "Name": "@ItemNo",
        "Type": "string",
        "Format": "item-number",
        "MinLength": 1,
        "MaxLength": 50
      }
    ]
  }
}
```

`RequiredRoles`, `AllowedTenants`, and typed `ArgumentContract` rules are enforced before adapter resolution.

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
