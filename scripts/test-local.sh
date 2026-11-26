#!/bin/sh
# Lokale Tests in der Sandbox von Claude Code (ADR-0020 §1, Owner-Entscheid vom 26.11.2026). Die CI nutzt dieses Skript nicht.
#
# Warum nicht einfach `dotnet build` und `dotnet test`:
# - Ohne Socket-Freigabe hängt `dotnet build`. Darum baut das Skript mit
#   --disable-build-servers -m:1 -nodeReuse:false.
# - `dotnet test` verbindet sich über einen Socket mit der Test-App und scheitert mit
#   "SocketException (13): Permission denied". Darum startet das Skript die vier Test-Apps direkt.
#
# Bekannte Grenze der Sandbox (nicht aufgeweicht, nicht ausgefiltert):
#   Monarch.Cli.Tests.HookSubagentStopShellsTests.Real_ps_finds_the_hull_of_a_real_child_and_the_hook_leaves_it_running
#   scheitert in der Sandbox, weil /bin/ps dort nicht starten darf ("Operation not permitted").
#   Der Hook selbst läuft außerhalb der Sandbox. In der CI ist der Test grün.
#
# Exitcode: 0 nur, wenn Build und alle vier Test-Apps grün sind; sonst 1.
set -u

root=$(cd "$(dirname "$0")/.." && pwd) || exit 1
cd "$root" || exit 1

DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_CLI_TELEMETRY_OPTOUT

echo "== dotnet build -warnaserror --disable-build-servers -m:1 -nodeReuse:false"
if ! dotnet build -warnaserror --disable-build-servers -m:1 -nodeReuse:false; then
  echo "test-local: Build fehlgeschlagen" >&2
  exit 1
fi

failed=""
for name in Domain Policy Infrastructure Cli; do
  app="tests/Monarch.$name.Tests/bin/Debug/net10.0/Monarch.$name.Tests"
  echo "== $app"
  if [ ! -x "$app" ]; then
    echo "test-local: $app fehlt" >&2
    failed="$failed $name"
    continue
  fi
  if ! "$app"; then
    failed="$failed $name"
  fi
done

if [ -n "$failed" ]; then
  echo "test-local: fehlgeschlagen:$failed" >&2
  echo "test-local: Hinweis: in der Sandbox scheitert Real_ps_finds_the_hull_of_a_real_child_and_the_hook_leaves_it_running (/bin/ps); das ist eine bekannte Grenze der Sandbox, kein Freibrief für andere Fehler." >&2
  exit 1
fi
echo "test-local: alle Test-Apps grün"
