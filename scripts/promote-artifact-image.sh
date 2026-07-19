#!/usr/bin/env bash
set -euo pipefail

readonly source_image="${1:?source image is required}"
readonly source_tag="${2:?source tag is required}"
readonly target_image="${3:?target image is required}"
readonly target_tag="${4:?target tag is required}"
readonly expected_digest="${5:-}"
readonly gcloud_bin="${GCLOUD_BIN:-gcloud}"
readonly docker_bin="${DOCKER_BIN:-docker}"
readonly not_found_status=44

is_digest() {
  [[ "$1" =~ ^sha256:[0-9a-f]{64}$ ]]
}

describe_digest() {
  local reference="$1"
  local allow_not_found="$2"
  local error_file
  local output
  local status

  error_file="$(mktemp)"
  set +e
  output="$("$gcloud_bin" artifacts docker images describe "$reference" \
    --format='value(image_summary.digest)' 2>"$error_file")"
  status=$?
  set -e

  if [[ "$status" -eq 0 ]]; then
    rm -f -- "$error_file"
    if ! is_digest "$output"; then
      printf 'Artifact Registry returned an invalid digest for %s: %s\n' "$reference" "$output" >&2
      return 2
    fi
    printf '%s\n' "$output"
    return 0
  fi

  if [[ "$allow_not_found" == true ]] && grep -Eq '(^|[[:space:]])NOT_FOUND:[[:space:]]' "$error_file"; then
    rm -f -- "$error_file"
    return "$not_found_status"
  fi

  cat "$error_file" >&2
  rm -f -- "$error_file"
  return "$status"
}

if source_digest="$(describe_digest "$source_image:$source_tag" false)"; then
  :
else
  status=$?
  exit "$status"
fi

if [[ -n "$expected_digest" ]]; then
  if ! is_digest "$expected_digest"; then
    printf 'Expected digest is invalid: %s\n' "$expected_digest" >&2
    exit 2
  fi
  if [[ "$source_digest" != "$expected_digest" ]]; then
    printf 'Source digest %s does not match approved digest %s\n' "$source_digest" "$expected_digest" >&2
    exit 1
  fi
fi

target_reference="$target_image:$target_tag"
target_digest=""
if target_digest="$(describe_digest "$target_reference" true)"; then
  if [[ "$target_digest" != "$source_digest" ]]; then
    printf 'Refusing to overwrite %s: %s != %s\n' "$target_reference" "$target_digest" "$source_digest" >&2
    exit 1
  fi
else
  status=$?
  if [[ "$status" -ne "$not_found_status" ]]; then
    exit "$status"
  fi

  "$docker_bin" buildx imagetools create \
    --tag "$target_reference" \
    "$source_image@$source_digest" >&2
fi

if promoted_digest="$(describe_digest "$target_reference" false)"; then
  :
else
  status=$?
  exit "$status"
fi

if [[ "$promoted_digest" != "$source_digest" ]]; then
  printf 'Promoted digest verification failed for %s: %s != %s\n' \
    "$target_reference" "$promoted_digest" "$source_digest" >&2
  exit 1
fi

printf '%s\n' "$promoted_digest"
