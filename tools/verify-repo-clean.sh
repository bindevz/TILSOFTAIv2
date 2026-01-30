#!/usr/bin/env bash
set -euo pipefail

patterns='(^|/)\.vs/|(^|/)(bin|obj)/|(^|/)TestResults/'
bad=$(git ls-files | grep -E "${patterns}" || true)

if [[ -n "${bad}" ]]; then
  echo "Tracked build artifacts found:"
  echo "${bad}"
  exit 1
fi

echo "Repo clean: no tracked build artifacts found."
