#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
API_PROJECT="${REPO_ROOT}/src/Jibo.Cloud/dotnet/src/Jibo.Cloud.Api/Jibo.Cloud.Api.csproj"

CERT_PEM="${CERT_PEM:-${REPO_ROOT}/src/Jibo.Cloud/node/cert.pem}"
KEY_PEM="${KEY_PEM:-${REPO_ROOT}/src/Jibo.Cloud/node/key.pem}"
CHAIN_PEM="${CHAIN_PEM:-}"
PFX_OUT="${PFX_OUT:-${REPO_ROOT}/.tmp/openjibo-dev-cert.pfx}"
PFX_PASSWORD="${PFX_PASSWORD:-}"
CERT_WITH_CHAIN_PEM="${CERT_WITH_CHAIN_PEM:-${REPO_ROOT}/.tmp/openjibo-dev-cert-chain.pem}"
ASPNETCORE_URLS="${ASPNETCORE_URLS:-https://0.0.0.0:443;http://0.0.0.0:24605}"
DOTNET_ENVIRONMENT="${DOTNET_ENVIRONMENT:-Development}"
CAPTURE_DIRECTORY="${CAPTURE_DIRECTORY:-${REPO_ROOT}/captures/websocket}"
PROTOCOL_CAPTURE_DIRECTORY="${PROTOCOL_CAPTURE_DIRECTORY:-${REPO_ROOT}/captures/http}"
STATE_DIRECTORY="${STATE_DIRECTORY:-${REPO_ROOT}/captures/state}"

mkdir -p "$(dirname "${PFX_OUT}")"
mkdir -p "${CAPTURE_DIRECTORY}"
mkdir -p "${PROTOCOL_CAPTURE_DIRECTORY}"
mkdir -p "${STATE_DIRECTORY}"

if [[ ! -f "${CERT_PEM}" ]]; then
  echo "Missing CERT_PEM: ${CERT_PEM}" >&2
  exit 1
fi

if [[ ! -f "${KEY_PEM}" ]]; then
  echo "Missing KEY_PEM: ${KEY_PEM}" >&2
  exit 1
fi

if [[ -n "${CHAIN_PEM}" ]]; then
  if [[ ! -f "${CHAIN_PEM}" ]]; then
    echo "Missing CHAIN_PEM: ${CHAIN_PEM}" >&2
    exit 1
  fi

  cat "${CERT_PEM}" "${CHAIN_PEM}" > "${CERT_WITH_CHAIN_PEM}"
  CERT_PFX_INPUT="${CERT_WITH_CHAIN_PEM}"
else
  CERT_PFX_INPUT="${CERT_PEM}"
fi

if [[ -z "${PFX_PASSWORD}" ]]; then
  PFX_PASSWORD="$(openssl rand -hex 16)"
fi

OPENSSL_ARGS=(
  pkcs12
  -export
  -out "${PFX_OUT}"
  -inkey "${KEY_PEM}"
  -in "${CERT_PFX_INPUT}"
  -passout "pass:${PFX_PASSWORD}"
)

echo "Creating PFX for Kestrel"
echo " - cert: ${CERT_PEM}"
echo " - key: ${KEY_PEM}"
if [[ -n "${CHAIN_PEM}" ]]; then
  echo " - chain: ${CHAIN_PEM}"
  echo " - cert with chain: ${CERT_WITH_CHAIN_PEM}"
fi
echo " - pfx: ${PFX_OUT}"
openssl "${OPENSSL_ARGS[@]}"

export ASPNETCORE_URLS
export DOTNET_ENVIRONMENT
export ASPNETCORE_Kestrel__Certificates__Default__Path="${PFX_OUT}"
export ASPNETCORE_Kestrel__Certificates__Default__Password="${PFX_PASSWORD}"
export OpenJibo__Telemetry__DirectoryPath="${CAPTURE_DIRECTORY}"
export OpenJibo__ProtocolTelemetry__DirectoryPath="${PROTOCOL_CAPTURE_DIRECTORY}"
: "${OpenJibo__State__PersistencePath:=${STATE_DIRECTORY}/cloud-state.json}"
: "${OpenJibo__PersonalMemory__PersistencePath:=${STATE_DIRECTORY}/personal-memory.json}"
export OpenJibo__State__PersistencePath
export OpenJibo__PersonalMemory__PersistencePath

resolve_dotnet() {
  if [[ -n "${DOTNET_ROOT:-}" && -x "${DOTNET_ROOT}/dotnet" ]]; then
    printf '%s\n' "${DOTNET_ROOT}/dotnet"
    return 0
  fi

  if command -v dotnet >/dev/null 2>&1; then
    command -v dotnet
    return 0
  fi

  local user_home="${HOME}"
  if [[ -n "${SUDO_USER:-}" ]]; then
    user_home="$(eval echo "~${SUDO_USER}")"
  fi

  local candidate
  for candidate in \
    "${user_home}/.dotnet/dotnet" \
    /usr/local/share/dotnet/dotnet \
    /usr/share/dotnet/dotnet
  do
    if [[ -x "${candidate}" ]]; then
      printf '%s\n' "${candidate}"
      return 0
    fi
  done

  echo "dotnet was not on PATH. Install the SDK or add ~/.dotnet to PATH." >&2
  echo "Under sudo, this machine usually has ${user_home}/.dotnet/dotnet." >&2
  exit 1
}

DOTNET_BIN="$(resolve_dotnet)"
DOTNET_ROOT="$(cd "$(dirname "${DOTNET_BIN}")" && pwd)"
export DOTNET_ROOT
export PATH="${DOTNET_ROOT}:${PATH}"

# sudo changes HOME to /var/root, which breaks whisper.cpp path probes and Metal caches.
if [[ -n "${SUDO_USER:-}" ]]; then
  REAL_HOME="$(eval echo "~${SUDO_USER}")"
  if [[ -d "${REAL_HOME}" ]]; then
    export HOME="${REAL_HOME}"
  fi
fi

# Remove stale root-owned build artifact directories that were created by a
# previous sudo build. They contain old auto-generated .cs files which cause
# CS0579 duplicate-attribute errors if left in place next to fresh obj dirs.
while IFS= read -r -d '' dir; do
  echo "Removing stale build dir: ${dir}"
  rm -rf "${dir}"
done < <(find "${REPO_ROOT}/src" -type d \( -name "*.rootowned" -o -name "*.rootowned.bak" \) -print0 2>/dev/null)

echo ""
echo "Starting OpenJibo .NET cloud"
echo " - project: ${API_PROJECT}"
echo " - urls: ${ASPNETCORE_URLS}"
echo " - environment: ${DOTNET_ENVIRONMENT}"
echo " - dotnet: ${DOTNET_BIN}"
echo " - websocket captures: ${CAPTURE_DIRECTORY}"
echo " - http captures: ${PROTOCOL_CAPTURE_DIRECTORY}"

cd "${REPO_ROOT}"
exec "${DOTNET_BIN}" run --project "${API_PROJECT}" --no-launch-profile
