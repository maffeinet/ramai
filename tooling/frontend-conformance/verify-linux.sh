#!/bin/sh
# Local review only; does not install or configure the candidate in the active application.
set -eu
export npm_config_cache=/tmp/ramai-npm-cache
export NEXT_TELEMETRY_DISABLED=1
export npm_config_update_notifier=false
if [ "$(npm --version)" != "11.17.0" ]; then
  npm install --prefix /tmp/ramai-npm npm@11.17.0 --ignore-scripts --strict-peer-deps
  export PATH="/tmp/ramai-npm/node_modules/.bin:$PATH"
fi
node --version
npm --version
uname -s
cat /etc/os-release
failed=0
run() {
  label=$1
  shift
  printf '\nSTAGE %s\n' "$label"
  if "$@"; then
    printf 'RESULT %s PASS\n' "$label"
  else
    code=$?
    printf 'RESULT %s FAIL exit=%s\n' "$label" "$code"
    failed=1
  fi
}
run baseline-install npm --prefix src/frontend/ramai-web ci --ignore-scripts --strict-peer-deps
run prototype-install npm --prefix tooling/frontend-conformance ci --ignore-scripts --strict-peer-deps
run prototype-lint npm --prefix tooling/frontend-conformance run lint
run prototype-conformance npm --prefix tooling/frontend-conformance run report
run prototype-audit npm --prefix tooling/frontend-conformance audit --json
run baseline-lint npm --prefix src/frontend/ramai-web run lint
run baseline-types npm --prefix src/frontend/ramai-web run type-check
run baseline-build npm --prefix src/frontend/ramai-web run build
run baseline-production-audit npm --prefix src/frontend/ramai-web audit --omit=dev --json
if [ -d /evidence ]; then
  for file in results.json tests.tap; do
    if [ -f "artifacts/frontend-conformance/$file" ]; then
      cp "artifacts/frontend-conformance/$file" "/evidence/linux-$file"
    fi
  done
fi
exit "$failed"
