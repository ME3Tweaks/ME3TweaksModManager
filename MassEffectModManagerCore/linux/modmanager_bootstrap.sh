#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TMP_DIR="$(mktemp -d)"

# DOWNLOAD AND EXTRACT ==================================================
prepareExe() {
    cleanup_tmp() {
        rm -rf "$TMP_DIR"
    }
    trap cleanup_tmp EXIT


    echo "==> Fetching latest ME3TweaksModManager release info..."
    API_URL="https://api.github.com/repos/ME3Tweaks/ME3TweaksModManager/releases/latest"
    RELEASE_JSON="$TMP_DIR/release.json"
    curl -fsSL "$API_URL" -o "$RELEASE_JSON"

    # Grab the (single) .exe asset's download URL without requiring jq
    EXE_URL=$(grep -o '"browser_download_url"[[:space:]]*:[[:space:]]*"[^"]*\.exe"' "$RELEASE_JSON" \
        | head -n1 \
        | sed -E 's/.*"(https[^"]+)"/\1/')

    if [[ -z "${EXE_URL:-}" ]]; then
        echo "Error: could not find a .exe asset in the latest release." >&2
        exit 1
    fi

    EXE_NAME=$(basename "$EXE_URL")
    EXTRACTOR_EXE="$SCRIPT_DIR/$EXE_NAME"

    echo "==> Downloading $EXE_NAME ..."
    curl -fsSL "$EXE_URL" -o "$EXTRACTOR_EXE"

    echo "==> Downloading 7-Zip (linux x64)..."
    SEVENZIP_TAR="$TMP_DIR/7z2602-linux-x64.tar.xz"
    curl -fsSL "https://github.com/ip7z/7zip/releases/download/26.02/7z2602-linux-x64.tar.xz" -o "$SEVENZIP_TAR"

    SEVENZIP_DIR="$TMP_DIR/7zip-extracted"
    mkdir -p "$SEVENZIP_DIR"
    tar -xf "$SEVENZIP_TAR" -C "$SEVENZIP_DIR"

    # Locate the 7zz binary regardless of internal tar layout
    SEVENZZ_BIN=$(find "$SEVENZIP_DIR" -type f -name '7zz' | head -n1)
    if [[ -z "${SEVENZZ_BIN:-}" ]]; then
        echo "Error: could not locate 7zz binary after extraction." >&2
        exit 1
    fi
    chmod +x "$SEVENZZ_BIN"

    echo "==> Extracting internal application exe from $EXE_NAME ..."
    "$SEVENZZ_BIN" x "$EXTRACTOR_EXE" -o"$SCRIPT_DIR" -y

    echo "==> Cleaning up download"
    rm -f "$EXTRACTOR_EXE"
    # 7zz binary, the 7z tarball, and the extracted 7z folder all live under
    # $TMP_DIR, which the EXIT trap removes automatically.
}

findPrefixes() {
    # Common default Steam installation paths on Linux
    STEAM_PATHS=(
        "$HOME/.local/share/Steam"
        "$HOME/.steam/steam"
        "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam" # Flatpak
    )

    find_compatdata() {
        local appid="$1"
        for steam_dir in "${STEAM_PATHS[@]}"; do
            local compat_path="$steam_dir/steamapps/compatdata/$appid/pfx"
            if [ -d "$compat_path" ]; then
                echo "$compat_path"
                return 0
            fi
        done
    
        # Check custom libraries via libraryfolders.vdf if it exists
        for steam_dir in "${STEAM_PATHS[@]}"; do
            local vdf_path="$steam_dir/steamapps/libraryfolders.vdf"
            if [ -f "$vdf_path" ]; then
                # Extract additional library paths from VDF
                while read -r lib_path; do
                    if [ -d "$lib_path/steamapps/compatdata/$appid/pfx" ]; then
                        echo "$lib_path/steamapps/compatdata/$appid/pfx"
                        return 0
                    fi
                done < <(grep -i '"path"' "$vdf_path" | awk -F'"' '{print $4}')
            fi
        done
    
        return 1
    }

    # Example usage: find prefix for AppID 400 (Portal)
    PREFIX_DIR=$(find_compatdata 400)
    if [ -n "$PREFIX_DIR" ]; then
        echo "Found prefix at: $PREFIX_DIR"
    else
        echo "Prefix not found."
    fi

}

# Find prefixes that have Mass Effect games in them. =================

# 


echo "==> Done. Contents of $SCRIPT_DIR:"
ls -la "$SCRIPT_DIR"