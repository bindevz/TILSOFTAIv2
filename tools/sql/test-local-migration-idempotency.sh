#!/usr/bin/env bash
set -euo pipefail

SERVER="localhost"
DATABASE="TILSOFTAI"
USER="sa"
PASSWORD="123"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --server)
      SERVER="$2"
      shift 2
      ;;
    --database)
      DATABASE="$2"
      shift 2
      ;;
    --user)
      USER="$2"
      shift 2
      ;;
    --password)
      PASSWORD="$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SQL_ROOT="$REPO_ROOT/sql/current"

if ! command -v sqlcmd >/dev/null 2>&1; then
  echo "sqlcmd was not found on PATH. Install SQL Server command line tools to run local migration idempotency tests." >&2
  exit 1
fi

validate_pack() {
  if [[ -f "$SQL_ROOT/998_validate_schema_contract.sql" ]]; then
    echo "Validating 998_validate_schema_contract.sql"
    sqlcmd -b -S "$SERVER" -d "$DATABASE" -U "$USER" -P "$PASSWORD" -C -i "$SQL_ROOT/998_validate_schema_contract.sql"
  fi

  echo "Validating 999_validate_model_runtime.sql"
  sqlcmd -b -S "$SERVER" -d "$DATABASE" -U "$USER" -P "$PASSWORD" -C -i "$SQL_ROOT/999_validate_model_runtime.sql"
}

seed_counts() {
  sqlcmd -b -S "$SERVER" -d "$DATABASE" -U "$USER" -P "$PASSWORD" -C -h -1 -W -s "," -Q "
SET NOCOUNT ON;
SELECT
    (SELECT COUNT(1) FROM dbo.Model WHERE TenantId = N'default' AND ModelCode IN (N'ABC', N'XYZ', N'SET-DINING-001')) AS ModelCount,
    (SELECT COUNT(1) FROM dbo.Material WHERE TenantId = N'default' AND MaterialCode IN (N'MAT-OAK', N'MAT-STL', N'MAT-LIN', N'MAT-FOAM')) AS MaterialCount,
    (SELECT COUNT(1) FROM dbo.ModelPiece WHERE TenantId = N'default') AS PieceCount,
    (SELECT COUNT(1) FROM dbo.ModelMaterial WHERE TenantId = N'default') AS MaterialLinkCount,
    (SELECT COUNT(1) FROM dbo.ModelPackagingOption WHERE TenantId = N'default') AS PackagingCount;
" | grep -E '^[0-9]+,[0-9]+,[0-9]+,[0-9]+,[0-9]+$' | head -n 1
}

echo "Idempotency pass 1: migrate"
"$SCRIPT_DIR/migrate-local-tilsoftai.sh" --server "$SERVER" --database "$DATABASE" --user "$USER" --password "$PASSWORD"
validate_pack
before_counts="$(seed_counts)"
echo "Seed counts after first migration: $before_counts"

echo "Idempotency pass 2: migrate"
"$SCRIPT_DIR/migrate-local-tilsoftai.sh" --server "$SERVER" --database "$DATABASE" --user "$USER" --password "$PASSWORD"
validate_pack
after_counts="$(seed_counts)"
echo "Seed counts after second migration: $after_counts"

if [[ "$before_counts" != "$after_counts" ]]; then
  echo "Seed row counts changed after repeated migration. Before=$before_counts After=$after_counts" >&2
  exit 1
fi

echo "Local SQL migration idempotency test passed."
