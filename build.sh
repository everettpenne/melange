#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

# Use a locally installed .NET SDK if present (see README), otherwise the system one.
if [ -x "./.dotnet/dotnet" ]; then
    DOTNET="$(pwd)/.dotnet/dotnet"
else
    DOTNET="dotnet"
fi

echo "Building with: $DOTNET"
"$DOTNET" publish Jellyfin.Plugin.Comments.csproj -c Release -o artifacts

echo
echo "Done. Plugin DLL: $(pwd)/artifacts/Jellyfin.Plugin.Comments.dll"
echo "Install to: <jellyfin-data>/plugins/Comments/Jellyfin.Plugin.Comments.dll"
