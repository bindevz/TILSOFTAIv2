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
  echo "sqlcmd was not found on PATH. Install SQL Server command line tools to run local SQL validation." >&2
  exit 1
fi

for name in 998_validate_schema_contract.sql 999_validate_model_runtime.sql; do
  path="$SQL_ROOT/$name"
  if [[ ! -f "$path" ]]; then
    echo "Validation SQL file not found: $path" >&2
    exit 1
  fi

  echo "Executing $name"
  sqlcmd -b -S "$SERVER" -d "$DATABASE" -U "$USER" -P "$PASSWORD" -C -i "$path"
done

echo "TILSOFTAI local SQL schema/runtime validation completed."
