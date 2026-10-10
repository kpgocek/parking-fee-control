#!/bin/bash

set -euo pipefail
cd "$(dirname "$0")"

BUILD_DIR="./cs-parking-fees/bin/Debug/net48"
GAME_MODS_DIR=""

if [ -f local.envs ]; then
    source local.envs
fi

case "$BUILD_DIR" in
    /*) ;;
    *) BUILD_DIR="$PWD/${BUILD_DIR#./}" ;;
esac

: "${GAME_MODS_DIR:?Set GAME_MODS_DIR in local.envs to the mod installation directory}"

# MSBuild assembles the DLL, UI metadata/assets and locales together.
dotnet build ParkingFeeControl.sln -c Debug \
    -p:OutputPath="$BUILD_DIR/" -p:AppendTargetFrameworkToOutputPath=false

test -f "$BUILD_DIR/ParkingFeeControl.dll"
test -f "$BUILD_DIR/ParkingFeeControl.mjs"
test -f "$BUILD_DIR/mod.json"
mkdir -p "$GAME_MODS_DIR"
cp -R "$BUILD_DIR/." "$GAME_MODS_DIR/"

# Retain the existing explicit clean option; ordinary builds preserve user fees.
if [ "${1:-}" = "clean" ]; then
    rm -f "$GAME_MODS_DIR/parking-config.json"
fi

echo "Mod assembled in $BUILD_DIR and installed to $GAME_MODS_DIR"
