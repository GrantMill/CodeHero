#!/usr/bin/env bash
set -euo pipefail

# Install reportgenerator if missing (idempotent)
dotnet tool restore || true
dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.0.0 || true
export PATH="$PATH:$HOME/.dotnet/tools"

# Find coverage file
COVERAGE_FILE=$(find . -type f \( -iname "coverage.cobertura.xml" -o -iname "coverage.xml" -o -iname "*.cobertura.xml" -o -iname "coverage.opencover.xml" \) -print -quit || true)
if [ -z "$COVERAGE_FILE" ]; then
  echo "Coverage file not found" >&2
  ls -R TestResults || true
  exit 1
fi

echo "Using coverage file: $COVERAGE_FILE"

# Generate a text summary (best-effort)
reportgenerator -reports:"$COVERAGE_FILE" -targetdir:"coverage-report" -reporttypes:TextSummary || true

# Try to compute coverage percent from Cobertura attributes
COVERAGE_PERCENT=""
if echo "$COVERAGE_FILE" | grep -qiE '\.xml$'; then
  LINES_COVERED=$(grep -oP 'lines-covered="\K[0-9]+' "$COVERAGE_FILE" | head -n1 || true)
  LINES_VALID=$(grep -oP 'lines-valid="\K[0-9]+' "$COVERAGE_FILE" | head -n1 || true)
  if [ -n "$LINES_COVERED" ] && [ -n "$LINES_VALID" ] && [ "$LINES_VALID" -gt 0 ]; then
    COVERAGE_PERCENT=$(awk "BEGIN {printf \"%.1f\", ($LINES_COVERED/$LINES_VALID)*100}")
  fi
fi

# Fallback to reportgenerator summary
if [ -z "$COVERAGE_PERCENT" ]; then
  if [ -f coverage-report/Summary.txt ]; then
    COVERAGE_PERCENT=$(grep -oP 'Coverage \(line\):\s+\K[0-9]+\.[0-9]+' coverage-report/Summary.txt | head -n1 || true)
  fi
fi

if [ -z "$COVERAGE_PERCENT" ]; then
  echo "Could not determine coverage percent" >&2
  exit 1
fi

echo "Coverage%: $COVERAGE_PERCENT"

# Global threshold - adjust if needed
# Default to 75, but if .github/coverage-thresholds.json contains a 'default' key use that
THRESHOLD=75
if [ -f .github/coverage-thresholds.json ]; then
  DEFAULT_TH=$(python3 - <<'PY'
import json
try:
    d=json.load(open('.github/coverage-thresholds.json'))
    v=d.get('default')
    if v is not None:
        print(int(v))
except Exception:
    pass
PY
  ) || true
  if [ -n "${DEFAULT_TH:-}" ]; then
    THRESHOLD=${DEFAULT_TH}
  fi
fi

COVERAGE_INT=${COVERAGE_PERCENT%%.*}
if [ "$COVERAGE_INT" -lt "$THRESHOLD" ]; then
  echo "Coverage $COVERAGE_PERCENT% is below threshold of $THRESHOLD%" >&2
  exit 1
fi

# Optional per-package thresholds
if [ -f .github/coverage-thresholds.json ]; then
  echo "Per-package thresholds file found: .github/coverage-thresholds.json"
  python3 .github/scripts/check_coverage.py "$COVERAGE_FILE"
  RC=$?
  if [ $RC -ne 0 ]; then
    echo "Per-package coverage checks failed" >&2
    exit $RC
  fi
else
  echo "No per-package thresholds file; using global threshold check above."
fi
