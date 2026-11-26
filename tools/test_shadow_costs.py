"""Tests für tools/shadow-costs.py (Q-0194)."""

import importlib.util
import io
import json
import statistics
import unittest
from contextlib import redirect_stderr, redirect_stdout
from datetime import datetime, timedelta, timezone
from pathlib import Path
from tempfile import TemporaryDirectory

ROOT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("shadow_costs", ROOT / "shadow-costs.py")
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

WINDOW = 1795371000
BASE = datetime(2026, 11, 1, 12, 0, 0, tzinfo=timezone.utc)
MODELS = {"warden": "opus", "knight": "sonnet"}


def iso(seconds: int) -> str:
    """Fester Zeitpunkt plus Sekunden, mit Offset."""
    return (BASE + timedelta(seconds=seconds)).isoformat()


def ev(seq, at, type, **payload):
    """Baut ein Ledger-Ereignis. payload-Felder werden zum Payload."""
    return {
        "seq": seq,
        "at": at,
        "actor": "test",
        "type": type,
        "payload": payload,
        "prevHash": "p",
        "hash": f"h{seq}",
    }


def snap(seq, at_s, five, seven, stale=False, window=WINDOW):
    """Frischer oder abgelaufener Mana-Snapshot im festen 5h-Fenster."""
    return ev(
        seq,
        iso(at_s),
        "ManaSnapshotTaken",
        stale=stale,
        fiveHourUsedPct=five,
        sevenDayUsedPct=seven,
        fiveHourResetsAt=window,
        sevenDayResetsAt=1,
    )


def tool(seq, at_s, agent, agent_type="warden"):
    """Start-Ereignis eines Shadows."""
    return ev(seq, iso(at_s), "ToolUsed", agent_id=agent, agent_type=agent_type)


def done(seq, at_s, agent, agent_type="warden"):
    """Ende eines Shadows."""
    return ev(seq, iso(at_s), "ShadowRunCompleted", agent_id=agent, agent_type=agent_type)


class ShadowCostsTests(unittest.TestCase):
    """Die zwölf Fälle aus dem Auftrag."""

    def test_clean_run_counts_with_costs(self):
        """Ein sauberer Lauf zählt 5h 8 und 7d 1."""
        events = [snap(1, 0, 10, 5), tool(2, 10, "a"), done(3, 50, "a"), snap(4, 60, 18, 6)]
        self.assertEqual(mod.measure(events, MODELS), {("warden", "opus"): [(8, 1)]})

    def test_gap_before_start_121_drops_120_counts(self):
        """121 s vor dem Start fällt der Lauf, 120 s zählt er."""
        def case(gap):
            start = 1000
            return [
                snap(1, start - gap, 10, 5),
                tool(2, start, "a"),
                done(3, start + 100, "a"),
                snap(4, start + 110, 18, 6),
            ]

        self.assertEqual(mod.measure(case(121), MODELS), {})
        self.assertEqual(mod.measure(case(120), MODELS), {("warden", "opus"): [(8, 1)]})

    def test_overlapping_runs_both_drop(self):
        """Zwei überlappende Läufe mit verschiedenen Messpaaren fallen beide."""
        events = [
            snap(1, 0, 0, 0),
            tool(2, 10, "a", "warden"),
            snap(3, 40, 1, 0),
            tool(4, 50, "b", "knight"),
            done(5, 80, "b", "knight"),
            snap(6, 90, 2, 0),
            done(7, 200, "a", "warden"),
            snap(8, 210, 3, 0),
        ]
        self.assertEqual(mod.measure(events, MODELS), {})

    def test_same_measurement_pair_both_drop(self):
        """Zwei Läufe nacheinander ohne Snapshot dazwischen teilen das Paar."""
        events = [
            snap(1, 0, 0, 0),
            tool(2, 10, "a"),
            done(3, 20, "a"),
            tool(4, 30, "b"),
            done(5, 40, "b"),
            snap(6, 50, 4, 1),
        ]
        self.assertEqual(mod.measure(events, MODELS), {})

    def test_negative_seven_day_delta_drops(self):
        """Eine negative 7d-Differenz fällt heraus."""
        events = [snap(1, 0, 10, 5), tool(2, 10, "a"), done(3, 20, "a"), snap(4, 30, 12, 4)]
        self.assertEqual(mod.measure(events, MODELS), {})

    def test_different_five_hour_window_drops(self):
        """Ein anderer fiveHourResetsAt danach fällt heraus."""
        events = [
            snap(1, 0, 10, 5),
            tool(2, 10, "a"),
            done(3, 20, "a"),
            snap(4, 30, 18, 6, window=WINDOW + 1),
        ]
        self.assertEqual(mod.measure(events, MODELS), {})

    def test_stale_snapshot_just_before_start_is_ignored(self):
        """stale direkt vor dem Start zählt nicht. Der frische davor nur bis 120 s."""
        start = 500
        counted = [
            snap(1, start - 120, 10, 5),
            snap(2, start - 1, 99, 5, stale=True),
            tool(3, start, "a"),
            done(4, start + 10, "a"),
            snap(5, start + 20, 18, 6),
        ]
        self.assertEqual(mod.measure(counted, MODELS), {("warden", "opus"): [(8, 1)]})
        dropped = [
            snap(1, start - 121, 10, 5),
            snap(2, start - 1, 10, 5, stale=True),
            tool(3, start, "a"),
            done(4, start + 10, "a"),
            snap(5, start + 20, 18, 6),
        ]
        self.assertEqual(mod.measure(dropped, MODELS), {})

    def test_same_event_from_two_sources_counts_once(self):
        """Dieselbe Zeile aus zwei Ordnern bleibt nach Dateiname, seq und hash eine."""
        line = json.dumps(
            {
                "seq": 1,
                "at": "2026-11-01T12:00:00+00:00",
                "actor": "test",
                "type": "ToolUsed",
                "payload": {"agent_id": "a"},
                "prevHash": "p",
                "hash": "abc",
            }
        )
        with TemporaryDirectory() as first, TemporaryDirectory() as second:
            for root in (first, second):
                folder = Path(root) / ".monarch" / "ledger" / "2026-11"
                folder.mkdir(parents=True)
                (folder / "same.jsonl").write_text(line + "\n", encoding="utf-8")
            events, broken = mod.load_events([first, second], True)
        self.assertEqual(broken, 0)
        self.assertEqual(len(events), 1)

    def test_nearest_rank_p90_and_median(self):
        """Für 1..13 ist p90 = 12. Der Median von 1, 2, 3, 4 ist 2,5."""
        self.assertEqual(mod.p90(list(range(1, 14))), 12)
        self.assertEqual(statistics.median([1, 2, 3, 4]), 2.5)

    def test_five_fractional_digits_are_read(self):
        """Fünf Nachkommastellen werden auf Mikrosekunden aufgefüllt."""
        got = mod.parse_at("2026-10-30T23:09:43.76155+00:00")
        self.assertEqual(got, datetime(2026, 10, 30, 23, 9, 43, 761550, tzinfo=timezone.utc))

    def test_agent_type_suffix_uses_model_map(self):
        """umbrarch:warden wird warden mit dem Modell aus models."""
        events = [
            snap(1, 0, 10, 5),
            tool(2, 10, "a", "umbrarch:knight"),
            done(3, 20, "a", "umbrarch:warden"),
            snap(4, 30, 18, 6),
        ]
        self.assertEqual(list(mod.measure(events, MODELS)), [("warden", "opus")])

    def test_main_prints_table_and_rejects_other_plans(self):
        """--no-git schreibt Kopf und Zeile. plan max5 endet mit Code 2."""
        with TemporaryDirectory() as repo:
            root = Path(repo)
            ledger = root / ".monarch" / "ledger" / "2026-11"
            ledger.mkdir(parents=True)
            (root / ".monarch" / "system.yaml").write_text("mana:\n  plan: pro\n", encoding="utf-8")
            rows = [snap(1, 0, 10, 5), tool(2, 10, "a"), done(3, 20, "a"), snap(4, 30, 18, 6)]
            (ledger / "run.jsonl").write_text("".join(json.dumps(row) + "\n" for row in rows), encoding="utf-8")
            out, err = io.StringIO(), io.StringIO()
            with redirect_stdout(out), redirect_stderr(err):
                code = mod.main(["--no-git", repo])
            text = out.getvalue()
            self.assertEqual(code, 0)
            self.assertIn("Ereignisse: 4, Läufe: 1, gezählt: 1", text)
            self.assertIn("| Shadow (Modell) | n | 5h Median | 5h p90 | 5h Max | 7d p90 |", text)
            self.assertIn("| warden (opus) | 1 | – | – | – | – |", text)
            (root / ".monarch" / "system.yaml").write_text("mana:\n  plan: max5\n", encoding="utf-8")
            with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
                code = mod.main(["--no-git", repo])
            self.assertEqual(code, 2)


if __name__ == "__main__":
    unittest.main()
