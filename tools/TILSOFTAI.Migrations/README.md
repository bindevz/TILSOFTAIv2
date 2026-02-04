# TILSOFTAI Database Migrations

## Usage

```bash
# Check status
dotnet run -- status --environment Development

# Apply migrations (dry run)
dotnet run -- migrate --dry-run

# Apply migrations
dotnet run -- migrate --environment Production

# With explicit connection string
dotnet run -- migrate --connection "Server=...;Database=TILSOFTAI;..."
```

## Script Naming Convention

- `001-099`: Core infrastructure
- `100-199`: Module tables and views  
- `200-299`: Module stored procedures
- `300-399`: Migrations and alterations
- `900-999`: Seed data

## Adding New Migrations

1. Create SQL file in appropriate `sql/` folder
2. Follow naming: `{order}_{type}_{description}.sql`
3. Ensure idempotency (CREATE OR ALTER, IF NOT EXISTS)
4. Run `dotnet run -- migrate --dry-run` to verify
