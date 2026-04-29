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
  echo "sqlcmd was not found on PATH. Install SQL Server command line tools to run local migration." >&2
  exit 1
fi

for script in "$SQL_ROOT"/*.sql; do
  name="$(basename "$script")"
  target_db="$DATABASE"
  if [[ "$name" == "000_create_database.sql" ]]; then
    target_db="master"
  fi

  echo "Executing $name"
  sqlcmd -b -S "$SERVER" -d "$target_db" -U "$USER" -P "$PASSWORD" -C -i "$script" -v "DatabaseName=$DATABASE"
done

echo "TILSOFTAI local SQL migration completed."
