#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION="2.3.41"
SDK_VERSION="10.0.401"
BUILD_ROOT="${RF4_BUILD_ROOT:-/data/rf4-build-env}"
DOTNET_ROOT="${DOTNET_ROOT:-$BUILD_ROOT/dotnet}"
SDK_ARCHIVE="${RF4_DOTNET_SDK_ARCHIVE:-$BUILD_ROOT/cache/dotnet-sdk-${SDK_VERSION}-linux-x64.tar.gz}"
NUGET_PACKAGES="${NUGET_PACKAGES:-$BUILD_ROOT/nuget-packages}"
OFFLINE_FEED="${RF4_OFFLINE_FEED:-$BUILD_ROOT/offline-feed}"
DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$BUILD_ROOT/dotnet-home}"
DIST="$ROOT/dist/RF4AssistantPro_v${VERSION}_win-x64"
ZIP="$ROOT/dist/RF4AssistantPro_v${VERSION}_win-x64.zip"

mkdir -p "$BUILD_ROOT/cache" "$DOTNET_ROOT" "$NUGET_PACKAGES" \
    "$DOTNET_CLI_HOME" "$ROOT/dist"

if [[ ! -x "$DOTNET_ROOT/dotnet" ]]; then
    if [[ ! -f "$SDK_ARCHIVE" ]]; then
        echo "ERROR: .NET SDK archive not found:"
        echo "  $SDK_ARCHIVE"
        echo
        echo "Place dotnet-sdk-${SDK_VERSION}-linux-x64.tar.gz there,"
        echo "or set RF4_DOTNET_SDK_ARCHIVE to its path."
        exit 2
    fi

    tar -xzf "$SDK_ARCHIVE" -C "$DOTNET_ROOT"
fi

export DOTNET_ROOT NUGET_PACKAGES DOTNET_CLI_HOME
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

actual_version="$(dotnet --version)"
if [[ "$actual_version" != 10.* ]]; then
    echo "ERROR: .NET 10 SDK is required; found $actual_version"
    exit 3
fi

restore_args=()
if [[ "${RF4_OFFLINE:-1}" == "1" ]]; then
    restore_args+=(--ignore-failed-sources -p:NuGetAudit=false)
    if [[ -d "$OFFLINE_FEED" ]]; then
        restore_args+=(--source "$OFFLINE_FEED")
    fi
fi

echo "[1/5] Automated scenarios"
dotnet restore \
    "$ROOT/RF4AssistantPro.Tests/RF4AssistantPro.Tests.csproj" \
    "${restore_args[@]}"
dotnet run \
    --project "$ROOT/RF4AssistantPro.Tests/RF4AssistantPro.Tests.csproj" \
    -c Release --no-restore

echo "[2/5] Restore for win-x64"
dotnet restore "$ROOT/RF4AssistantPro.sln" \
    -r win-x64 --disable-parallel "${restore_args[@]}"

echo "[3/5] Release build for win-x64"
dotnet build "$ROOT/RF4AssistantPro.sln" \
    -c Release --no-restore

echo "[4/5] Self-contained win-x64 publish"
rm -rf "$DIST"
dotnet publish "$ROOT/RF4AssistantPro/RF4AssistantPro.csproj" \
    -c Release \
    -r win-x64 \
    --self-contained true \
    --no-restore \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -o "$DIST"

test -f "$DIST/RF4AssistantPro.exe"
test "$(find "$DIST/Assets/Ocr/Paddle" -maxdepth 1 -name '*.onnx' | wc -l)" -eq 3
find "$DIST" -type f \( -name '*.pdb' -o -name '*.lib' \) -delete
cp "$ROOT/Launch/START_RF4_ASSISTANT.cmd" "$DIST/"
cp "$ROOT/Launch/START_DIAGNOSTICS.cmd" "$DIST/"
cp "$ROOT/Launch/README_START.txt" "$DIST/"

echo "[5/5] ZIP and SHA-256"
rm -f "$ZIP" "$ZIP.sha256"
(
    cd "$ROOT/dist"
    zip -qr "$ZIP" "$(basename "$DIST")"
)
sha256sum "$ZIP" | sed "s#  .*/#  #" > "$ZIP.sha256"

echo
echo "BUILD COMPLETED"
echo "EXE: $DIST/RF4AssistantPro.exe"
echo "ZIP: $ZIP"
echo "SHA: $ZIP.sha256"