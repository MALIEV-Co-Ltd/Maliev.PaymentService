#!/usr/bin/env bash
set -euo pipefail

readonly repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
readonly promotion_script="$repository_root/scripts/promote-artifact-image.sh"
readonly source_digest="sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
readonly conflicting_digest="sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
readonly source_image="registry.example/project/source/payment"
readonly target_image="registry.example/project/target/payment"
readonly source_tag="dev-0123456789abcdef"
readonly target_tag="1.2.3"

readonly temp_root="$(mktemp -d)"
trap 'rm -rf -- "$temp_root"' EXIT

cat > "$temp_root/gcloud" <<'MOCK_GCLOUD'
#!/usr/bin/env bash
set -euo pipefail
reference="${5:?expected image reference as fifth gcloud argument}"
if [[ "$reference" == "$MOCK_SOURCE_IMAGE:$MOCK_SOURCE_TAG" ]]; then
  printf '%s\n' "$MOCK_SOURCE_DIGEST"
  exit 0
fi
if [[ "$reference" != "$MOCK_TARGET_IMAGE:$MOCK_TARGET_TAG" ]]; then
  printf 'ERROR: unexpected image reference %s\n' "$reference" >&2
  exit 2
fi
if [[ -f "$MOCK_PROMOTED_STATE" ]]; then
  printf '%s\n' "$MOCK_SOURCE_DIGEST"
  exit 0
fi
case "$MOCK_TARGET_MODE" in
  absent)
    printf 'ERROR: (gcloud.artifacts.docker.images.describe) NOT_FOUND: Requested entity was not found.\n' >&2
    exit 1
    ;;
  same)
    printf '%s\n' "$MOCK_SOURCE_DIGEST"
    ;;
  conflict)
    printf '%s\n' "$MOCK_CONFLICTING_DIGEST"
    ;;
  failure)
    printf 'ERROR: (gcloud.artifacts.docker.images.describe) PERMISSION_DENIED: denied\n' >&2
    exit 1
    ;;
  *)
    printf 'ERROR: unsupported mock mode %s\n' "$MOCK_TARGET_MODE" >&2
    exit 2
    ;;
esac
MOCK_GCLOUD

cat > "$temp_root/docker" <<'MOCK_DOCKER'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$MOCK_DOCKER_LOG"
test "$1 $2 $3" = "buildx imagetools create"
touch "$MOCK_PROMOTED_STATE"
MOCK_DOCKER
chmod +x "$temp_root/gcloud" "$temp_root/docker"

run_case() {
  local mode="$1"
  local expected_status="$2"
  local output_file="$temp_root/$mode.out"
  local error_file="$temp_root/$mode.err"
  local docker_log="$temp_root/$mode.docker.log"
  local promoted_state="$temp_root/$mode.promoted"
  local status=0

  : > "$docker_log"
  rm -f -- "$promoted_state"
  set +e
  MOCK_TARGET_MODE="$mode" \
    MOCK_SOURCE_IMAGE="$source_image" \
    MOCK_TARGET_IMAGE="$target_image" \
    MOCK_SOURCE_TAG="$source_tag" \
    MOCK_TARGET_TAG="$target_tag" \
    MOCK_SOURCE_DIGEST="$source_digest" \
    MOCK_CONFLICTING_DIGEST="$conflicting_digest" \
    MOCK_DOCKER_LOG="$docker_log" \
    MOCK_PROMOTED_STATE="$promoted_state" \
    GCLOUD_BIN="$temp_root/gcloud" \
    DOCKER_BIN="$temp_root/docker" \
    bash "$promotion_script" \
      "$source_image" "$source_tag" "$target_image" "$target_tag" "$source_digest" \
      >"$output_file" 2>"$error_file"
  status=$?
  set -e

  if [[ "$expected_status" == success ]]; then
    test "$status" -eq 0
    grep -Fxq "$source_digest" "$output_file"
  else
    test "$status" -ne 0
  fi

  case "$mode" in
    absent)
      grep -Fq "buildx imagetools create" "$docker_log"
      ;;
    same)
      test ! -s "$docker_log"
      ;;
    conflict)
      test ! -s "$docker_log"
      grep -Fq "Refusing to overwrite" "$error_file"
      ;;
    failure)
      test ! -s "$docker_log"
      grep -Fq "PERMISSION_DENIED" "$error_file"
      ;;
  esac
}

run_case absent success
run_case same success
run_case conflict failure
run_case failure failure

printf 'promotion helper behavior tests passed\n'
