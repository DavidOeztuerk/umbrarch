#!/bin/sh
# Tests scripts/check-plugin-version.sh (ADR-0021 §3) in throwaway git repos.
# Usage: plugin/tests/plugin-version-test.sh   (POSIX sh; needs git)
here=$(cd "$(dirname "$0")" && pwd)
script=$here/../../scripts/check-plugin-version.sh
work=$(mktemp -d "${TMPDIR:-/tmp}/plugin-version.XXXXXX") || exit 2
trap 'rm -rf "$work"' EXIT
fails=0
check() { # name, expected exit, actual exit
  if [ "$2" = "$3" ]; then echo "ok   - $1"; else echo "FAIL - $1 (expected $2, got $3)"; fails=$((fails + 1)); fi
}

# repo <dir> <version>: plugin.json, wrapper and McpTools.cs carry <version>; one commit, branch base
repo() {
  d=$1
  mkdir -p "$d/plugin/.claude-plugin" "$d/plugin/scripts" "$d/plugin/skills" "$d/src/Monarch.Cli"
  git -C "$d" init -q -b develop
  setv "$d" "$2" "$2" "$2"
  echo text >"$d/plugin/skills/a.md"
  echo other >"$d/README.md"
  git -C "$d" add -A
  git -C "$d" -c user.email=t@t -c user.name=t -c commit.gpgsign=false commit -q -m base
  git -C "$d" branch base
}

# setv <dir> <plugin.json> <wrapper> <mcp>
setv() {
  printf '{\n  "name": "umbrarch",\n  "version": "%s",\n  "license": "x"\n}\n' "$2" >"$1/plugin/.claude-plugin/plugin.json"
  printf '#!/bin/sh\nMONARCH_VERSION=%s\nmode=x\n' "$3" >"$1/plugin/scripts/monarch"
  printf 'class X\n{\n    private const string ServerVersion = "%s";\n}\n' "$4" >"$1/src/Monarch.Cli/McpTools.cs"
}

commit() {
  git -C "$1" add -A
  git -C "$1" -c user.email=t@t -c user.name=t -c commit.gpgsign=false commit -q -m change
}

run() { (cd "$1" && sh "$script" base >/dev/null 2>&1); echo $?; }

# 1. plugin/ changed, version not raised: refused
r=$work/r1; repo "$r" 0.1.2; echo changed >"$r/plugin/skills/a.md"; commit "$r"
check "plugin/ changed without a higher version" 1 "$(run "$r")"

# 2. plugin/ changed, version raised everywhere: ok
r=$work/r2; repo "$r" 0.1.2; echo changed >"$r/plugin/skills/a.md"; setv "$r" 0.1.3 0.1.3 0.1.3; commit "$r"
check "plugin/ changed with a higher version" 0 "$(run "$r")"

# 3. only files outside plugin/ changed: ok without a new version
r=$work/r3; repo "$r" 0.1.2; echo changed >"$r/README.md"; commit "$r"
check "no change under plugin/" 0 "$(run "$r")"

# 4. plugin/ changed, version lowered: refused
r=$work/r4; repo "$r" 0.1.2; echo changed >"$r/plugin/skills/a.md"; setv "$r" 0.1.1 0.1.1 0.1.1; commit "$r"
check "plugin/ changed with a lower version" 1 "$(run "$r")"

# 5. versions differ between plugin.json, wrapper and McpTools.cs: refused, also without a change under plugin/
r=$work/r5; repo "$r" 0.1.2; setv "$r" 0.1.3 0.1.3 0.1.2; echo changed >"$r/plugin/skills/a.md"; commit "$r"
check "McpTools.cs differs" 1 "$(run "$r")"
r=$work/r6; repo "$r" 0.1.2; setv "$r" 0.1.2 0.1.3 0.1.2; commit "$r"
check "wrapper differs (plugin/ changed)" 1 "$(run "$r")"

# 6. version 0.1.10 is higher than 0.1.9 (numeric, not text order)
r=$work/r7; repo "$r" 0.1.9; echo changed >"$r/plugin/skills/a.md"; setv "$r" 0.1.10 0.1.10 0.1.10; commit "$r"
check "0.1.10 is higher than 0.1.9" 0 "$(run "$r")"

[ "$fails" = 0 ] && echo "all plugin version tests passed" || echo "$fails plugin version test(s) failed"
exit "$fails"
