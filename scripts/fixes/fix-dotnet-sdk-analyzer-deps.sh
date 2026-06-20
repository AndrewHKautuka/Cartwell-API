#!/usr/bin/env bash
#
# fix-dotnet-sdk-analyzer-deps.sh
#
# Works around a .NET SDK packaging gap (seen with apt-installed SDKs on
# Debian/Ubuntu-based distros) where Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll
# is missing its System.Composition.AttributedModel.dll dependency. This causes
# tools that load the SDK's built-in analyzers via MEF (e.g. `jb cleanupcode`,
# `dotnet format`) to throw FileNotFoundException for analyzers such as
# CSharpAvoidDuplicateAcceleratorsFixer.
#
# What it does:
#   1. Finds every Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll under the SDK root.
#   2. For each one missing System.Composition.AttributedModel.dll alongside it,
#      finds a matching copy from elsewhere in the SAME SDK version (e.g. the
#      bundled dotnet-format tool) and copies it in.
#   3. Leaves anything already correct untouched (safe to re-run any time).
#
# Re-run this after any `apt upgrade` that touches dotnet-sdk-*, since the
# package update will overwrite the analyzer folder and drop the file again.
#
# Usage:
#   ./fix-dotnet-sdk-analyzer-deps.sh
#   DOTNET_ROOT=/custom/path ./fix-dotnet-sdk-analyzer-deps.sh

set -euo pipefail

DOTNET_ROOT="${DOTNET_ROOT:-/usr/lib/dotnet}"
MISSING_DLL="System.Composition.AttributedModel.dll"

if [ ! -d "$DOTNET_ROOT" ]; then
    echo "ERROR: DOTNET_ROOT '$DOTNET_ROOT' does not exist." >&2
    exit 1
fi

echo "==> Scanning $DOTNET_ROOT for SDKs affected by missing $MISSING_DLL"
echo ""

mapfile -t analyzer_dlls < <(find "$DOTNET_ROOT" -iname "Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll" 2>/dev/null)

if [ ${#analyzer_dlls[@]} -eq 0 ]; then
    echo "No Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll found under $DOTNET_ROOT."
    echo "Nothing to fix (or DOTNET_ROOT is wrong)."
    exit 0
fi

fixed=0
already_ok=0
unresolved=0

for dll in "${analyzer_dlls[@]}"; do
    target_dir=$(dirname "$dll")

    if [ -f "$target_dir/$MISSING_DLL" ]; then
        echo "OK    $target_dir"
        already_ok=$((already_ok + 1))
        continue
    fi

    # Pull the SDK version segment out of the path, e.g. .../sdk/10.0.108/...
    sdk_version=$(printf '%s' "$dll" | grep -oP '(?<=/sdk/)[0-9]+\.[0-9]+\.[0-9]+' || true)

    if [ -z "$sdk_version" ]; then
        echo "SKIP  $target_dir (could not determine SDK version from path)"
        unresolved=$((unresolved + 1))
        continue
    fi

    # Look for a copy of the missing DLL anywhere else within the SAME SDK
    # version, so the assembly version is guaranteed to match what the
    # analyzer expects (e.g. the copy bundled with dotnet-format).
    source_dll=$(find "$DOTNET_ROOT/sdk/$sdk_version" -iname "$MISSING_DLL" 2>/dev/null | head -1)

    if [ -z "$source_dll" ]; then
        echo "FAIL  $target_dir"
        echo "      No copy of $MISSING_DLL found anywhere under sdk/$sdk_version"
        unresolved=$((unresolved + 1))
        continue
    fi

    echo "FIX   $target_dir"
    echo "      copying from: $source_dll"

    if [ -w "$target_dir" ]; then
        cp "$source_dll" "$target_dir/"
    else
        sudo cp "$source_dll" "$target_dir/"
    fi

    fixed=$((fixed + 1))
done

echo ""
echo "==> Summary: fixed=$fixed already_ok=$already_ok unresolved=$unresolved"

if [ "$unresolved" -gt 0 ]; then
    echo ""
    echo "Some folders could not be fixed automatically — no matching copy of"
    echo "$MISSING_DLL exists anywhere in that SDK installation. You may need to"
    echo "reinstall/repair that SDK version, or source the file from a machine"
    echo "with a working install of the same SDK version."
    exit 1
fi

exit 0
