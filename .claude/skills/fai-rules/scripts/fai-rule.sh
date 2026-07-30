#!/usr/bin/env bash
# fai-rule.sh — retrieve single sections of the verbatim FAI source rules
# without reading whole 100k-token documents into context.
#
# Usage:
#   fai-rule.sh show <ref>            print just that rule section (+ descendants)
#   fai-rule.sh find <regex> [vol]    list rule refs whose text matches
#   fai-rule.sh toc <vol> [prefix]    list rule refs/headings (optionally filtered)
#   fai-rule.sh check-links           verify every docs/rules anchor resolves
#
#   <ref> examples: F3B.1.5  F3J.10.5  F3K.9.6  5.5.11.12  5.5.12.11.1  C.16.2.6
#   <vol>          : f3 | f5 | ciam   (inferred from <ref> in `show`)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../../.." && pwd)"
RULES_DIR="$REPO_ROOT/docs/rules"
SRC_DIR="$RULES_DIR/source-docs"

die() { printf 'fai-rule: %s\n' "$1" >&2; exit 1; }

volume_file() {
  case "$1" in
    f3)   echo "$SRC_DIR/f3-soaring-2025.md" ;;
    f5)   echo "$SRC_DIR/f5-electric-2026.md" ;;
    ciam) echo "$SRC_DIR/ciam-general-rules-2026.md" ;;
    *)    die "unknown volume '$1' (expected f3, f5 or ciam)" ;;
  esac
}

# Route a rule ref to its volume: F3x.* -> f3, 5.5.* -> f5, C.* -> ciam
volume_of_ref() {
  case "$1" in
    F3*|f3*) echo f3 ;;
    5.5*)    echo f5 ;;
    C.*|c.*) echo ciam ;;
    *)       die "cannot infer volume from ref '$1' (expected F3B.1.5, 5.5.11.12 or C.16.2.6)" ;;
  esac
}

# Normalise case: class prefixes are uppercase in the source headings.
norm_ref() {
  printf '%s' "$1" | awk '{ if (tolower($0) ~ /^(f3|c\.)/) print toupper($0); else print }'
}

cmd_show() {
  [ $# -ge 1 ] || die "show needs a rule ref, e.g. 'show 5.5.11.12'"
  local ref file
  ref="$(norm_ref "$1")"
  file="$(volume_file "$(volume_of_ref "$ref")")"

  awk -v ref="$ref" '
    BEGIN { FS = " " }
    # heading line: "#+ <ref> <title>"
    /^#+ / {
      tok = $2
      if (!found) {
        if (tok == ref) { found = 1; print; next }
      } else {
        # keep printing descendants (ref.x.y), stop at the next sibling/parent
        if (index(tok, ref ".") != 1) exit
        print; next
      }
    }
    found { print }
    END { if (!found) exit 3 }
  ' "$file" || die "rule '$ref' not found in $(basename "$file") — try: fai-rule.sh toc $(volume_of_ref "$ref") ${ref%.*}"
}

cmd_find() {
  [ $# -ge 1 ] || die "find needs a regex, e.g. 'find \"landing bonus\"'"
  local pat="$1"; shift
  local files=()
  if [ $# -ge 1 ]; then files=("$(volume_file "$1")"); else files=("$SRC_DIR"/*.md); fi

  for file in "${files[@]}"; do
    awk -v pat="$pat" -v vol="$(basename "$file")" '
      /^#+ / { ref = $2; title = $0; sub(/^#+ +[^ ]+ +/, "", title); next }
      tolower($0) ~ tolower(pat) {
        key = ref
        if (!(key in seen)) { seen[key] = 1; printf "%-14s %-46.46s %s\n", ref, title, vol }
      }
    ' "$file"
  done
}

cmd_toc() {
  [ $# -ge 1 ] || die "toc needs a volume (f3 | f5 | ciam)"
  local file prefix
  file="$(volume_file "$1")"
  prefix="$(norm_ref "${2:-}")"
  awk -v prefix="$prefix" '
    /^#+ / {
      ref = $2; title = $0; sub(/^#+ +[^ ]+ +/, "", title)
      if (prefix == "" || index(ref, prefix) == 1) printf "%-16s %s\n", ref, title
    }
  ' "$file"
}

# Every "(source-docs/<file>#<anchor>)" link in docs/rules must resolve to a real
# heading. Guards the condensed docs against silent link rot.
cmd_check_links() {
  local rc=0 n=0
  local link file anchor tmp
  tmp="$(mktemp -d)"
  trap 'rm -rf "$tmp"' RETURN

  # Pre-compute the GitHub-style anchor set for each source doc once, so the
  # per-link check is a plain lookup (a `grep -q` inside a pipeline would
  # SIGPIPE its producer and, under `pipefail`, fake a broken link).
  anchors_for() {
    local f="$1" cache
    cache="$tmp/$(basename "$f").anchors"
    [ -f "$cache" ] || grep '^#' "$RULES_DIR/$f" \
      | sed 's/^#* //; s/[^A-Za-z0-9 -]//g; s/ /-/g' \
      | tr 'A-Z' 'a-z' > "$cache"
    echo "$cache"
  }

  while IFS= read -r link; do
    file="${link%%#*}"; anchor="${link#*#}"
    [ "$link" = "$file" ] && continue   # bare file link, no anchor
    if [ ! -f "$RULES_DIR/$file" ]; then
      printf 'BROKEN  missing file: %s\n' "$file"; rc=1; continue
    fi
    n=$((n + 1))
    if ! grep -qFx -- "$anchor" "$(anchors_for "$file")"; then
      printf 'BROKEN  %s#%s\n' "$file" "$anchor"; rc=1
    fi
  done < <(grep -oh '(source-docs/[^)]*)' "$RULES_DIR"/*.md | tr -d '()' | sort -u)

  [ $rc -eq 0 ] && echo "OK — $n source-doc anchors in docs/rules all resolve"
  return $rc
}

case "${1:-}" in
  show)        shift; cmd_show "$@" ;;
  find)        shift; cmd_find "$@" ;;
  toc)         shift; cmd_toc "$@" ;;
  check-links) shift; cmd_check_links ;;
  *) sed -n '2,15p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 1 ;;
esac
