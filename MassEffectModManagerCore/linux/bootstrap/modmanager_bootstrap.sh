#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TMP_DIR="$(mktemp -d)"

DEFAULT_APP_IDS=(17460 24980 2362420 1238020 1328670)
STEAM_PATH_OVERRIDE=""
CUSTOM_APP_IDS=()
RECONFIGURE_EXE_PATH=""
RESET_MODE="false"

usage() {
    cat <<'EOF'
Usage: modmanager_bootstrap.sh [--steam-path PATH] [--app-ids IDS] [--reconfigure PATH] [--reset]

Options:
  --steam-path PATH   Explicit Steam root directory (contains steamapps and appcache).
  --app-ids IDS       Comma-separated or space-separated AppIDs. May be passed multiple times.
    --reconfigure PATH  Use an existing ME3TweaksModManager.exe and skip download/extract.
    --reset             Skip download and remove launch option + CompatToolMapping for target AppIDs.
  -h, --help          Show this help message.

If no --app-ids values are provided, defaults are used:
  17460, 24980, 2362420, 1238020, 1328670
EOF
}

parse_args() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --steam-path)
                if [[ $# -lt 2 ]]; then
                    echo "Error: --steam-path requires a value." >&2
                    exit 2
                fi
                STEAM_PATH_OVERRIDE="$2"
                shift 2
                ;;
            --app-ids)
                if [[ $# -lt 2 ]]; then
                    echo "Error: --app-ids requires a value." >&2
                    exit 2
                fi
                CUSTOM_APP_IDS+=("$2")
                shift 2
                ;;
            --reconfigure)
                if [[ $# -lt 2 ]]; then
                    echo "Error: --reconfigure requires a path to ME3TweaksModManager.exe." >&2
                    exit 2
                fi
                RECONFIGURE_EXE_PATH="$2"
                shift 2
                ;;
            --reset)
                RESET_MODE="true"
                shift
                ;;
            -h|--help)
                usage
                exit 0
                ;;
            *)
                CUSTOM_APP_IDS+=("$1")
                shift
                ;;
        esac
    done
}

cleanup_tmp() {
    rm -rf "$TMP_DIR"
}
trap cleanup_tmp EXIT

normalize_steam_root() {
    local candidate="$1"

    if [[ -d "$candidate/steamapps" && -f "$candidate/appcache/appinfo.vdf" ]]; then
        printf '%s\n' "$candidate"
        return 0
    fi

    if [[ -d "$candidate/steam/steamapps" && -f "$candidate/steam/appcache/appinfo.vdf" ]]; then
        printf '%s\n' "$candidate/steam"
        return 0
    fi

    return 1
}

discover_steam_root() {
    local candidates=()
    local normalized=""

    if [[ -n "$STEAM_PATH_OVERRIDE" ]]; then
        candidates+=("$STEAM_PATH_OVERRIDE")
    else
        candidates+=(
            "$HOME/.steam"
            "$HOME/.steam/steam"
            "$HOME/.local/share/Steam"
            "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam"
        )
    fi

    for candidate in "${candidates[@]}"; do
        if [[ -d "$candidate" ]]; then
            normalized="$(normalize_steam_root "$candidate" || true)"
            if [[ -n "$normalized" ]]; then
                printf '%s\n' "$normalized"
                return 0
            fi
        fi
    done

    return 1
}

steam_command() {
    local steam_root="$1"

    if [[ -x "$steam_root/steam.sh" ]]; then
        printf '%s\n' "$steam_root/steam.sh"
        return 0
    fi

    if command -v steam >/dev/null 2>&1; then
        command -v steam
        return 0
    fi

    return 1
}

shutdown_steam() {
    local steam_bin="$1"

    if ! pgrep -x 'steam|steamwebhelper|steam.sh' >/dev/null 2>&1; then
        echo "==> Steam is not running, skipping shutdown."
        return 0
    fi

    echo "==> Requesting Steam shutdown..."
    "$steam_bin" -shutdown >/dev/null 2>&1 || true

    local max_wait=60
    local elapsed=0
    while pgrep -x 'steam|steamwebhelper|steam.sh' >/dev/null 2>&1; do
        if (( elapsed >= max_wait )); then
            echo "Warning: Steam processes are still running after ${max_wait}s." >&2
            break
        fi
        sleep 1
        elapsed=$((elapsed + 1))
    done

    if (( elapsed < max_wait )); then
        echo "==> Steam shutdown complete."
    fi
}

collect_library_paths() {
    local steam_root="$1"
    local -n out_ref=$2
    local library_vdf="$steam_root/steamapps/libraryfolders.vdf"

    out_ref=("$steam_root")
    if [[ -f "$library_vdf" ]]; then
        while IFS= read -r lib_path; do
            lib_path="${lib_path//\\\\/\\}"
            [[ -n "$lib_path" ]] && out_ref+=("$lib_path")
        done < <(grep -E '"path"[[:space:]]+"' "$library_vdf" | awk -F'"' '{print $4}')
    fi
}

resolve_target_ids() {
    local -n out_ref=$1
    out_ref=()

    if [[ ${#CUSTOM_APP_IDS[@]} -eq 0 ]]; then
        out_ref=("${DEFAULT_APP_IDS[@]}")
        return 0
    fi

    local raw token
    for raw in "${CUSTOM_APP_IDS[@]}"; do
        raw="${raw//,/ }"
        for token in $raw; do
            if [[ "$token" =~ ^[0-9]+$ ]]; then
                out_ref+=("$token")
            else
                echo "Error: invalid AppID '$token'." >&2
                return 1
            fi
        done
    done
}

find_prefixes() {
    local -n libraries_ref=$1
    local -n appids_ref=$2
    local -n found_ids_ref=$3
    local -n found_paths_ref=$4

    found_ids_ref=()
    found_paths_ref=()

    local appid lib candidate
    for appid in "${appids_ref[@]}"; do
        for lib in "${libraries_ref[@]}"; do
            candidate="$lib/steamapps/compatdata/$appid/pfx"
            if [[ -d "$candidate" ]]; then
                found_ids_ref+=("$appid")
                found_paths_ref+=("$candidate")
                break
            fi
        done
    done
}

game_name_for_appid() {
    local appid="$1"
    case "$appid" in
        17460)
            printf '%s\n' 'Mass Effect (2008)'
            ;;
        24980)
            printf '%s\n' 'ME2 (2010, Steam)'
            ;;
        2362420)
            printf '%s\n' 'ME2 (2010, EA App)'
            ;;
        1238020)
            printf '%s\n' 'ME3 (2012)'
            ;;
        1328670)
            printf '%s\n' 'Mass Effect Legendary Edition'
            ;;
        *)
            printf '%s\n' 'Unknown Title'
            ;;
    esac
}

# DOWNLOAD AND EXTRACT ==================================================
prepareExe() {
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

find_modmanager_exe() {
    local exe_candidate

    if [[ -f "$SCRIPT_DIR/ME3TweaksModManager/ME3TweaksModManager.exe" ]]; then
        printf '%s\n' "$SCRIPT_DIR/ME3TweaksModManager/ME3TweaksModManager.exe"
        return 0
    fi

    exe_candidate="$(find "$SCRIPT_DIR" -maxdepth 1 -type f -name '*.exe' | head -n1 || true)"
    if [[ -n "$exe_candidate" ]]; then
        printf '%s\n' "$exe_candidate"
        return 0
    fi

    return 1
}

validate_reconfigure_exe() {
    local input_path="$1"
    local abs_path

    if [[ -z "$input_path" ]]; then
        echo "Error: --reconfigure requires a path value." >&2
        return 1
    fi

    abs_path="$(realpath "$input_path" 2>/dev/null || true)"
    if [[ -z "$abs_path" || ! -f "$abs_path" ]]; then
        echo "Error: reconfigure executable not found: $input_path" >&2
        return 1
    fi

    if [[ "$(basename "$abs_path")" != "ME3TweaksModManager.exe" ]]; then
        echo "Error: --reconfigure path must point to a file named ME3TweaksModManager.exe." >&2
        return 1
    fi

    printf '%s\n' "$abs_path"
}

ensure_python3() {
    if ! command -v python3 >/dev/null 2>&1; then
        echo "Error: python3 must be installed for setup to continue." >&2
        exit 1
    fi
}

main() {
    if [[ "$(uname -s)" != "Linux" ]]; then
        echo "Error: this bootstrap script supports Linux only." >&2
        exit 1
    fi

    parse_args "$@"
    ensure_python3

    if [[ "$RESET_MODE" == "true" && -n "$RECONFIGURE_EXE_PATH" ]]; then
        echo "Error: --reset and --reconfigure cannot be used together." >&2
        exit 2
    fi

    local target_ids
    resolve_target_ids target_ids

    local steam_root
    steam_root="$(discover_steam_root || true)"
    if [[ -z "$steam_root" ]]; then
        echo "Error: Steam must be installed for setup to continue." >&2
        exit 1
    fi
    echo "==> Using Steam root: $steam_root"

    local steam_bin
    steam_bin="$(steam_command "$steam_root" || true)"
    if [[ -z "$steam_bin" ]]; then
        echo "Error: could not locate Steam executable to perform shutdown." >&2
        exit 1
    fi
    shutdown_steam "$steam_bin"

    local libraries
    collect_library_paths "$steam_root" libraries

    local found_ids found_paths
    find_prefixes libraries target_ids found_ids found_paths

    if [[ ${#found_ids[@]} -eq 0 ]]; then
        echo "No matching prefixes found for requested AppIDs."
    else
        echo "==> Found prefixes:"
        local i game_name
        for i in "${!found_ids[@]}"; do
            game_name="$(game_name_for_appid "${found_ids[$i]}")"
            echo "  - AppID ${found_ids[$i]} (${game_name}): ${found_paths[$i]}"
        done
    fi

    local mm_exe
    if [[ "$RESET_MODE" == "true" ]]; then
        echo "==> Reset mode enabled; skipping download and extraction."
        mm_exe=""
    elif [[ -n "$RECONFIGURE_EXE_PATH" ]]; then
        echo "==> Reconfigure mode enabled; skipping download and extraction."
        mm_exe="$(validate_reconfigure_exe "$RECONFIGURE_EXE_PATH" || true)"
    else
        prepareExe
        mm_exe="$(find_modmanager_exe || true)"
    fi

    if [[ "$RESET_MODE" != "true" && -z "$mm_exe" ]]; then
        echo "Error: could not locate ME3TweaksModManager executable." >&2
        exit 1
    fi

    local config_vdf="$steam_root/config/config.vdf"
    if [[ ! -f "$config_vdf" ]]; then
        echo "Warning: config.vdf was not found at expected path: $config_vdf"
    fi

    if [[ "$RESET_MODE" == "true" ]]; then
        echo "==> Removing launch option and compat mappings..."
        python3 "$SCRIPT_DIR/me3tweaks_launch_option.py" \
            --pfx-ids "${target_ids[@]}" \
            --steam-path "$steam_root" \
            --compat-tool proton_11 \
            --config-vdf "$config_vdf" \
            --reset
    else
        local injection_ids=("${target_ids[@]}")
        if [[ ${#found_ids[@]} -gt 0 ]]; then
            injection_ids=("${found_ids[@]}")
        fi

        echo "==> Injecting launch option and Proton mapping..."
        python3 "$SCRIPT_DIR/me3tweaks_launch_option.py" \
            --pfx-ids "${injection_ids[@]}" \
            --exe "$mm_exe" \
            --steam-path "$steam_root" \
            --compat-tool proton_11 \
            --config-vdf "$config_vdf"
    fi

    echo "==> Done."
}

main "$@"