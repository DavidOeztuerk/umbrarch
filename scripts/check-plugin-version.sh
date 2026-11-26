#!/bin/sh
# Versionspflicht für plugin/ (ADR-0021 §3): Claude Code erneuert die Cache-Kopie des Plugins nur bei neuer Version.
# Usage: scripts/check-plugin-version.sh <basis-ref>   (im Repo; Exit 0 ok, 1 Befund, 2 Aufruffehler)
set -u
base=${1:-}
[ -n "$base" ] || { echo "usage: scripts/check-plugin-version.sh <basis-ref>" >&2; exit 2; }
root=$(git rev-parse --show-toplevel) || exit 2
cd "$root" || exit 2

json_version() { sed -n 's/^[[:space:]]*"version":[[:space:]]*"\([^"]*\)".*/\1/p' | head -n1; }

manifest=$(json_version <plugin/.claude-plugin/plugin.json)
wrapper=$(sed -n 's/^MONARCH_VERSION=//p' plugin/scripts/monarch | head -n1)
mcp=$(sed -n 's/.*ServerVersion = "\([^"]*\)".*/\1/p' src/Monarch.Cli/McpTools.cs | head -n1)
echo "plugin.json=$manifest wrapper=$wrapper McpTools.cs=$mcp"
if [ -z "$manifest" ] || [ "$manifest" != "$wrapper" ] || [ "$manifest" != "$mcp" ]; then
  echo "Befund: Die Version muss in plugin/.claude-plugin/plugin.json, plugin/scripts/monarch und src/Monarch.Cli/McpTools.cs gleich sein." >&2
  exit 1
fi

git rev-parse -q --verify "$base^{commit}" >/dev/null || { echo "Basis $base nicht gefunden" >&2; exit 2; }
if git diff --quiet "$base" HEAD -- plugin/; then
  echo "plugin/ unverändert gegenüber $base"
  exit 0
fi

old=$(git show "$base:plugin/.claude-plugin/plugin.json" | json_version)
top=$(printf '%s\n%s\n' "$old" "$manifest" | sort -V | tail -n1)
if [ "$manifest" = "$old" ] || [ "$top" != "$manifest" ]; then
  echo "Befund: plugin/ ist gegenüber $base geändert, die Version $manifest ist nicht höher als $old. Version erhöhen (plugin.json, Wrapper, McpTools.cs), sonst erneuert /plugin install die Cache-Kopie nicht." >&2
  exit 1
fi
echo "plugin/ geändert, Version $old -> $manifest"
