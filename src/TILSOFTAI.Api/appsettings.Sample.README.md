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
