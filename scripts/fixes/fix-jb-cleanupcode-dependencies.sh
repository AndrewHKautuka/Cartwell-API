#!/usr/bin/env bash
#
# fix-jb-cleanupcode-dependencies.sh
#
# Generalized fix for "Could not load file or assembly 'X, Version=Y...'"
# failures from `jb cleanupcode`. These happen because JetBrains.ReSharper.
# GlobalTools bundles its own private copies of certain dependencies (under
# ~/.nuget/packages/jetbrains.resharper.globaltools/<version>/tools/net8.0/any/Packages/)
# at fixed versions, while the .NET SDK's own NetAnalyzers expect an assembly
# version pinned to the exact installed SDK patch (e.g. 10.0.0.9 for SDK
# 10.0.109). When they don't match, the bundled copy is rejected with a
# FileNotFoundException even though a same-named file is sitting right there.
#
# This script:
#   1. Parses a cleanupcode output log for every distinct assembly name named
#      in a "Could not load file or assembly '<name>, Version=...'" message.
#   2. For each one, finds a matching DLL bundled with your installed .NET SDK
#      (under DotnetTools, e.g. dotnet-format/dotnet-watch) — guaranteed to be
#      the exact version your SDK expects.
#   3. Finds every place JetBrains' GlobalTools bundles its own copy of that
#      same-named dependency, and overwrites it with the SDK's copy.
#
# Re-run this any time the SDK patches itself (the required version string
# changes with every SDK update) or whenever a new "Could not load file or
# assembly" error shows up in your cleanupcode output.
#
# Usage:
#   ./fix-jb-cleanupcode-dependencies.sh <path-to-cleanupcode-output-log>
#
# Example:
#   dotnet jb cleanupcode Cartwell-API.slnx &> jb-cleanupcode-output
#   ./fix-jb-cleanupcode-dependencies.sh jb-cleanupcode-output
#   dotnet jb cleanupcode Cartwell-API.slnx   # re-run to confirm

set -euo pipefail

LOG_FILE="${1:-}"
DOTNET_ROOT="${DOTNET_ROOT:-/usr/lib/dotnet}"
TOOLS_DIR="${TOOLS_DIR:-$HOME/.nuget/packages/jetbrains.resharper.globaltools}"

if [ -z "$LOG_FILE" ] || [ ! -f "$LOG_FILE" ]; then
    echo "Usage: $0 <path-to-cleanupcode-output-log>" >&2
    exit 1
fi

if [ ! -d "$TOOLS_DIR" ]; then
    echo "ERROR: TOOLS_DIR '$TOOLS_DIR' does not exist." >&2
    echo "If your tool manifest installs locally rather than at this path, set TOOLS_DIR explicitly." >&2
    exit 1
fi

echo "==> Parsing $LOG_FILE for missing-assembly errors"

mapfile -t missing < <(grep -oP "Could not load file or assembly '\K[^,]+(?=,)" "$LOG_FILE" | sort -u)

if [ ${#missing[@]} -eq 0 ]; then
    echo "No 'Could not load file or assembly' errors found. Nothing to do."
    exit 0
fi

echo "Found ${#missing[@]} distinct missing assembly reference(s):"
printf '  - %s\n' "${missing[@]}"
echo ""

fixed=0
unresolved=0

for asm in "${missing[@]}"; do
    dll_name="${asm}.dll"

    echo "--- $asm ---"

    # Prefer a copy bundled with one of the SDK's own DotnetTools (dotnet-format,
    # dotnet-watch, etc.) since those ship at exactly the installed SDK's version.
    source_dll=$(find "$DOTNET_ROOT" -iname "$dll_name" -path "*DotnetTools*" 2>/dev/null | head -1)
    if [ -z "$source_dll" ]; then
        source_dll=$(find "$DOTNET_ROOT" -iname "$dll_name" 2>/dev/null | head -1)
    fi

    if [ -z "$source_dll" ]; then
        echo "FAIL  no source copy of $dll_name found anywhere under $DOTNET_ROOT"
        unresolved=$((unresolved + 1))
        continue
    fi

    echo "source: $source_dll"

    # Find every JetBrains-bundled probe location for this same dependency,
    # across every installed GlobalTools version.
    mapfile -t targets < <(find "$TOOLS_DIR" -ipath "*Packages/$asm/*/lib/*/$dll_name" 2>/dev/null)

    if [ ${#targets[@]} -eq 0 ]; then
        echo "FAIL  no JetBrains-bundled probe path found for $asm under $TOOLS_DIR"
        unresolved=$((unresolved + 1))
        continue
    fi

    for target in "${targets[@]}"; do
        echo "FIX   $target"
        cp "$source_dll" "$target"
    done

    fixed=$((fixed + 1))
    echo ""
done

echo "==> Summary: fixed=$fixed unresolved=$unresolved"

if [ "$unresolved" -gt 0 ]; then
    echo ""
    echo "Some dependencies could not be resolved automatically — either no matching"
    echo "copy exists in your SDK install, or JetBrains doesn't bundle that dependency"
    echo "under the expected Packages/ layout. Re-run cleanupcode and inspect the"
    echo "remaining error text for these by hand."
    exit 1
fi

exit 0
