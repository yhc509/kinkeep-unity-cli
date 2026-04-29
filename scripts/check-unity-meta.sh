#!/usr/bin/env bash
set -euo pipefail

upm_root="unity-package"

if [[ ! -d "$upm_root" ]]; then
  echo "UPM root not found: $upm_root" >&2
  exit 1
fi

missing_tmp="$(mktemp)"
orphan_tmp="$(mktemp)"
trap 'rm -f "$missing_tmp" "$orphan_tmp"' EXIT

package_roots=()
while IFS= read -r -d '' manifest; do
  package_roots+=("${manifest%/package.json}")
done < <(find "$upm_root" -type f -name package.json -print0)

if [[ ${#package_roots[@]} -eq 0 ]]; then
  echo "No UPM package.json files found under $upm_root" >&2
  exit 1
fi

is_excluded_path() {
  # Unity does not import tilde-suffixed UPM folders such as Tests~ and Samples~.
  case "$1" in
    *~ | *~/*) return 0 ;;
    *) return 1 ;;
  esac
}

package_root_for() {
  local path="$1"
  local root

  for root in "${package_roots[@]}"; do
    if [[ "$path" == "$root" || "$path" == "$root/"* ]]; then
      printf '%s\n' "$root"
      return 0
    fi
  done

  return 1
}

record_missing_meta() {
  printf 'MISSING META: %s\n' "$1" >> "$missing_tmp"
}

record_missing_directory_meta() {
  printf 'MISSING DIRECTORY META: %s\n' "$1" >> "$missing_tmp"
}

record_orphan_meta() {
  printf 'ORPHAN META: %s (missing %s)\n' "$1" "$2" >> "$orphan_tmp"
}

while IFS= read -r -d '' path; do
  if is_excluded_path "$path"; then
    continue
  fi

  if [[ ! -f "$path.meta" ]]; then
    record_missing_meta "$path"
  fi
done < <(find "$upm_root" -type f \( -name '*.cs' -o -name '*.asmdef' \) -print0)

while IFS= read -r -d '' path; do
  if is_excluded_path "$path"; then
    continue
  fi

  if root="$(package_root_for "$path")"; then
    if [[ "$path" != "$root" && ! -f "$path.meta" ]]; then
      record_missing_directory_meta "$path"
    fi
  fi
done < <(find "$upm_root" -type d -print0)

while IFS= read -r -d '' meta; do
  if is_excluded_path "$meta"; then
    continue
  fi

  original="${meta%.meta}"
  if [[ ! -e "$original" ]]; then
    record_orphan_meta "$meta" "$original"
  fi
done < <(find "$upm_root" -type f -name '*.meta' -print0)

if [[ -s "$missing_tmp" || -s "$orphan_tmp" ]]; then
  echo "Unity .meta validation failed."

  if [[ -s "$missing_tmp" ]]; then
    echo
    sort -u "$missing_tmp"
  fi

  if [[ -s "$orphan_tmp" ]]; then
    echo
    sort -u "$orphan_tmp"
  fi

  exit 1
fi

echo "Unity .meta validation passed."
