#!/usr/bin/env python3
"""Misst Mana-Kosten je Shadow aus Ledger-Snapshots (ADR-0020 §2).

Aufruf: python3 tools/shadow-costs.py [--agents-dir DIR] [--no-git] REPO [REPO ...]
"""

from __future__ import annotations

import argparse
import json
import re
import statistics
import subprocess
import sys
from datetime import datetime
from pathlib import Path
from typing import Optional

DASH = "\u2013"
REQUIRED = ("seq", "at", "actor", "type", "payload", "prevHash", "hash")
START_TYPES = ("ToolUsed", "PolicyDecided")
PLAN_RE = re.compile(r"^\s+plan:\s*(\S+)")
MODEL_RE = re.compile(r"^\s*model:\s*(\S+)\s*$")
# Python 3.9 liest nur 3 oder 6 Nachkommastellen; der Offset bleibt stehen.
FRAC_RE = re.compile(r"\.(\d+)(?=[+-]\d{2}:\d{2}$)")


class GitError(Exception):
    """Ein lesender Git-Befehl ist fehlgeschlagen."""


class Run:
    """Ein Shadow-Lauf vom ersten Start bis zum ersten Ende danach."""

    def __init__(self, name: str, start: datetime, end: datetime, prev: Optional[dict], nxt: Optional[dict]) -> None:
        """Merkt sich Name, Zeiten und die beiden Snapshot-Objekte."""
        self.name = name
        self.start = start
        self.end = end
        self.prev = prev
        self.nxt = nxt


def parse_at(text: str) -> datetime:
    """Liest einen ISO-Zeitpunkt mit Offset als aware datetime."""
    match = FRAC_RE.search(text)
    if match and len(match.group(1)) < 6:
        text = text[: match.start(1)] + match.group(1).ljust(6, "0") + text[match.end(1) :]
    stamp = datetime.fromisoformat(text)
    if stamp.tzinfo is None:
        raise ValueError("Zeit ohne Offset")
    return stamp


def p90(values: list[int]) -> int:
    """Nearest-Rank p90: Wert an Position ceil(0.9 * n), 1-basiert."""
    ordered = sorted(values)
    # ceil(0.9 * n) = ceil(9n/10). Ganzzahlig, damit float-ceil nicht verrutscht.
    pos = (9 * len(ordered) + 9) // 10
    return ordered[pos - 1]


def format_number(value: float) -> str:
    """Gibt eine ganze Zahl ohne Nachkomma aus, sonst mit einer Stelle."""
    number = float(value)
    if number.is_integer():
        return str(int(number))
    return f"{number:.1f}"


def shadow_name(agent_type: str) -> str:
    """Der Teil nach dem letzten Doppelpunkt; leer wird 'unbekannt'."""
    tail = agent_type.rsplit(":", 1)[-1].strip()
    return tail or "unbekannt"


def run_name(end_payload: dict, start_payload: dict) -> str:
    """Nimmt agent_type vom Ende, und nur ohne Wert den vom Start."""
    raw = end_payload.get("agent_type")
    if not isinstance(raw, str) or not raw.strip():
        raw = start_payload.get("agent_type")
    if not isinstance(raw, str):
        return "unbekannt"
    return shadow_name(raw)


def is_int(value: object) -> bool:
    """True für eine ganze Zahl. bool zählt nicht, weil es ein int ist."""
    return isinstance(value, int) and not isinstance(value, bool)


def fresh_snapshot(event: dict) -> bool:
    """Frischer Mana-Snapshot mit den Feldern, die ein Messpaar braucht."""
    if event.get("type") != "ManaSnapshotTaken":
        return False
    payload = event.get("payload")
    if not isinstance(payload, dict) or payload.get("stale") is not False:
        return False
    if not is_int(payload.get("fiveHourUsedPct")) or not is_int(payload.get("sevenDayUsedPct")):
        return False
    return payload.get("fiveHourResetsAt") is not None and payload.get("sevenDayResetsAt") is not None


def stamp_of(event: dict) -> Optional[datetime]:
    """Aware Zeit aus at, oder None wenn die Zeile keine trägt."""
    raw = event.get("at")
    if not isinstance(raw, str):
        return None
    try:
        return parse_at(raw)
    except ValueError:
        return None


def build_runs(events: list[dict]) -> list[Run]:
    """Baut je agent_id höchstens einen Lauf und hängt das Messpaar an."""
    snapshots: list[tuple[datetime, dict]] = []
    by_agent: dict[str, list[tuple[datetime, dict]]] = {}
    for event in events:
        at = stamp_of(event)
        if at is None:
            continue
        if fresh_snapshot(event):
            snapshots.append((at, event))
        if event.get("type") not in START_TYPES and event.get("type") != "ShadowRunCompleted":
            continue
        payload = event.get("payload")
        if not isinstance(payload, dict):
            continue
        agent_id = payload.get("agent_id")
        if not isinstance(agent_id, str) or not agent_id:
            continue
        by_agent.setdefault(agent_id, []).append((at, event))
    snapshots.sort(key=lambda item: item[0])
    runs: list[Run] = []
    for items in by_agent.values():
        starts = [item for item in items if item[1].get("type") in START_TYPES]
        if not starts:
            continue
        start_at, start_event = min(starts, key=lambda item: item[0])
        ends = [item for item in items if item[1].get("type") == "ShadowRunCompleted" and item[0] >= start_at]
        if not ends:
            continue
        end_at, end_event = min(ends, key=lambda item: item[0])
        prev = None
        for at, event in snapshots:
            if at <= start_at:
                prev = event
            else:
                break
        nxt = None
        for at, event in snapshots:
            if at >= end_at:
                nxt = event
                break
        name = run_name(end_event.get("payload") or {}, start_event.get("payload") or {})
        runs.append(Run(name, start_at, end_at, prev, nxt))
    return runs


def excluded_indexes(runs: list[Run]) -> set[int]:
    """Indizes von Läufen, die überlappen oder sich ein Messpaar teilen."""
    bad: set[int] = set()
    for i, left in enumerate(runs):
        for j in range(i + 1, len(runs)):
            right = runs[j]
            # Auch ein Lauf, der selbst nicht zählt, sperrt den anderen.
            if left.start < right.end and right.start < left.end:
                bad.add(i)
                bad.add(j)
    groups: dict[tuple[int, int], list[int]] = {}
    for i, run in enumerate(runs):
        if run.prev is None or run.nxt is None:
            continue
        groups.setdefault((id(run.prev), id(run.nxt)), []).append(i)
    for indexes in groups.values():
        if len(indexes) > 1:
            bad.update(indexes)
    return bad


def counted_costs(runs: list[Run], models: dict[str, str]) -> dict[tuple[str, str], list[tuple[int, int]]]:
    """Filtert die Läufe und sammelt (5h, 7d) je Name und Modell."""
    skip = excluded_indexes(runs)
    result: dict[tuple[str, str], list[tuple[int, int]]] = {}
    for i, run in enumerate(runs):
        if i in skip or run.prev is None or run.nxt is None:
            continue
        before = run.prev["payload"]
        after = run.nxt["payload"]
        if (run.start - stamp_of(run.prev)).total_seconds() > 120:
            continue
        if (stamp_of(run.nxt) - run.end).total_seconds() > 120:
            continue
        if before["fiveHourResetsAt"] != after["fiveHourResetsAt"]:
            continue
        delta_7d = after["sevenDayUsedPct"] - before["sevenDayUsedPct"]
        if delta_7d < 0:
            continue
        delta_5h = after["fiveHourUsedPct"] - before["fiveHourUsedPct"]
        model = models.get(run.name, "unbekannt")
        result.setdefault((run.name, model), []).append((delta_5h, delta_7d))
    return result


def analyze(events: list[dict], models: dict[str, str]) -> tuple[int, dict[tuple[str, str], list[tuple[int, int]]]]:
    """Liefert die Zahl aller Läufe und die gezählten Kosten."""
    runs = build_runs(events)
    return len(runs), counted_costs(runs, models)


def measure(events: list[dict], models: dict[str, str]) -> dict[tuple[str, str], list[tuple[int, int]]]:
    """Zählt die Kosten (5h, 7d) je (Name, Modell). Ohne Dateizugriff."""
    return analyze(events, models)[1]


def count_runs(events: list[dict]) -> int:
    """Zahl der Läufe mit Start und Ende, auch der nicht gezählten."""
    return analyze(events, {})[0]


def read_plan(repo: Path) -> Optional[str]:
    """Erste eingerückte plan-Zeile aus .monarch/system.yaml, sonst None."""
    path = repo / ".monarch" / "system.yaml"
    if not path.is_file():
        return None
    for line in path.read_text(encoding="utf-8").splitlines():
        match = PLAN_RE.match(line)
        if match:
            return match.group(1)
    return None


def parse_jsonl(filename: str, text: str) -> tuple[list[dict], int]:
    """Zerlegt Ledger-Text. Kaputte Zeilen werden gezählt und übersprungen."""
    events: list[dict] = []
    broken = 0
    for line in text.splitlines():
        if line == "":
            continue
        try:
            obj = json.loads(line)
        except json.JSONDecodeError:
            broken += 1
            continue
        if not isinstance(obj, dict) or any(key not in obj for key in REQUIRED) or not isinstance(obj["payload"], dict):
            broken += 1
            continue
        if stamp_of(obj) is None:
            broken += 1
            continue
        obj["_file"] = filename
        events.append(obj)
    return events, broken


def dedupe(events: list[dict]) -> list[dict]:
    """Behält je (Dateiname, seq, hash) das erste Ereignis."""
    seen = set()
    unique: list[dict] = []
    for event in events:
        # Dieselbe Zeile liegt im Arbeitsordner und auf mehreren Branches.
        key = (event.get("_file"), event.get("seq"), event.get("hash"))
        if key in seen:
            continue
        seen.add(key)
        unique.append(event)
    return unique


def workdir_files(repo: Path) -> list[Path]:
    """jsonl-Ledger im Arbeitsordner und in den Worktrees der ersten Ebene."""
    found: list[Path] = []
    ledger = repo / ".monarch" / "ledger"
    if ledger.is_dir():
        found.extend(path for path in ledger.rglob("*.jsonl") if path.is_file())
    worktrees = repo / ".claude" / "worktrees"
    if worktrees.is_dir():
        for entry in worktrees.iterdir():
            nested = entry / ".monarch" / "ledger"
            if entry.is_dir() and nested.is_dir():
                found.extend(path for path in nested.rglob("*.jsonl") if path.is_file())
    return found


def git_text(repo: str, args: list[str]) -> str:
    """Führt einen der erlaubten lesenden Git-Befehle aus."""
    proc = subprocess.run(
        ["git", "-C", repo, *args],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if proc.returncode != 0:
        detail = proc.stderr.strip() or f"Exit {proc.returncode}"
        raise GitError(f"{repo}: git {' '.join(args)} fehlgeschlagen: {detail}")
    return proc.stdout


def git_ledgers(repo: str) -> list[tuple[str, str]]:
    """Liest .jsonl-Blobs unter .monarch/ledger von jedem Branch, je SHA einmal."""
    refs = git_text(repo, ["for-each-ref", "--format=%(refname)", "refs/heads", "refs/remotes/origin"]).splitlines()
    cache: dict[str, str] = {}
    found: list[tuple[str, str]] = []
    seen: set[tuple[str, str]] = set()
    for ref in refs:
        ref = ref.strip()
        if not ref:
            continue
        listing = git_text(repo, ["ls-tree", "-r", ref, "--", ".monarch/ledger"])
        for line in listing.splitlines():
            meta, sep, path = line.partition("\t")
            if not sep:
                continue
            parts = meta.split(" ")
            if len(parts) < 3 or parts[1] != "blob" or not path.endswith(".jsonl"):
                continue
            sha = parts[2]
            name = path.rsplit("/", 1)[-1]
            if (sha, name) in seen:
                continue
            seen.add((sha, name))
            if sha not in cache:
                cache[sha] = git_text(repo, ["cat-file", "-p", sha])
            found.append((name, cache[sha]))
    return found


def load_events(repos: list[str], no_git: bool) -> tuple[list[dict], int]:
    """Liest und dedupliziert die Ledger aller REPOs. Mit no_git ohne Branches."""
    collected: list[dict] = []
    broken = 0
    for repo in repos:
        root = Path(repo)
        for path in workdir_files(root):
            try:
                text = path.read_text(encoding="utf-8")
            except (OSError, UnicodeDecodeError):
                broken += 1
                continue
            events, bad = parse_jsonl(path.name, text)
            collected.extend(events)
            broken += bad
        if no_git:
            continue
        for name, text in git_ledgers(repo):
            events, bad = parse_jsonl(name, text)
            collected.extend(events)
            broken += bad
    return dedupe(collected), broken


def default_agents_dir() -> Path:
    """plugin/agents relativ zur Repo-Wurzel, in der dieses Skript liegt."""
    return Path(__file__).resolve().parent.parent / "plugin" / "agents"


def load_models(agents_dir: Path) -> dict[str, str]:
    """Liest die Zeile model: aus jeder Datei <name>.md."""
    models: dict[str, str] = {}
    if not agents_dir.is_dir():
        return models
    for path in sorted(agents_dir.glob("*.md")):
        model = None
        try:
            lines = path.read_text(encoding="utf-8").splitlines()
        except (OSError, UnicodeDecodeError):
            models[path.stem] = "unbekannt"
            continue
        for line in lines:
            match = MODEL_RE.match(line)
            if match:
                model = match.group(1)
                break
        models[path.stem] = model or "unbekannt"
    return models


def render(event_count: int, run_count: int, measured: dict[tuple[str, str], list[tuple[int, int]]]) -> str:
    """Baut die Texttabelle. Shadows ohne gezählten Lauf fehlen."""
    counted = sum(len(costs) for costs in measured.values())
    lines = [
        f"Ereignisse: {event_count}, Läufe: {run_count}, gezählt: {counted}",
        "",
        "| Shadow (Modell) | n | 5h Median | 5h p90 | 5h Max | 7d p90 |",
        "|---|---|---|---|---|---|",
    ]
    rows = [(name, model, costs) for (name, model), costs in measured.items() if costs]
    rows.sort(key=lambda row: (-len(row[2]), row[0], row[1]))
    for name, model, costs in rows:
        n = len(costs)
        if n < 2:
            median = p90_5h = maximum = p90_7d = DASH
        else:
            fives = [item[0] for item in costs]
            sevens = [item[1] for item in costs]
            median = format_number(statistics.median(fives))
            p90_5h = format_number(p90(fives))
            maximum = format_number(max(fives))
            p90_7d = format_number(p90(sevens))
        lines.append(f"| {name} ({model}) | {n} | {median} | {p90_5h} | {maximum} | {p90_7d} |")
    return "\n".join(lines) + "\n"


def main(argv: Optional[list[str]] = None) -> int:
    """Prüft die Argumente, misst und schreibt die Tabelle nach stdout."""
    parser = argparse.ArgumentParser(
        prog="shadow-costs.py",
        description="Misst die Mana-Kosten je Shadow aus den Ledger-Snapshots.",
    )
    parser.add_argument("--agents-dir", default=None, help="Ordner der Agent-Definitionen. Default: plugin/agents neben dem Skript.")
    parser.add_argument("--no-git", action="store_true", help="Nur Arbeitsordner lesen, keine Branches.")
    parser.add_argument("repos", nargs="*", metavar="REPO", help="Projektwurzel mit .monarch/.")
    try:
        args = parser.parse_args(argv)
    except SystemExit as exc:
        if exc.code is None:
            return 0
        return exc.code if isinstance(exc.code, int) else 2
    if not args.repos:
        print("Mindestens ein REPO ist nötig.", file=sys.stderr)
        return 2
    for repo in args.repos:
        if not (Path(repo) / ".monarch").is_dir():
            print(f"{repo}: kein .monarch/", file=sys.stderr)
            return 2
        plan = read_plan(Path(repo))
        if plan != "pro":
            shown = plan if plan is not None else "fehlt"
            print(f"{repo}: Plan ist {shown}, gemessen wird nur pro.", file=sys.stderr)
            return 2
    try:
        events, broken = load_events(args.repos, args.no_git)
    except GitError as exc:
        print(str(exc), file=sys.stderr)
        return 2
    agents = Path(args.agents_dir) if args.agents_dir else default_agents_dir()
    total, measured = analyze(events, load_models(agents))
    sys.stdout.write(render(len(events), total, measured))
    print(f"Kaputte Zeilen: {broken}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
