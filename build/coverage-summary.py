#!/usr/bin/env python3
"""Summarize .NET code coverage and enforce the committed floors.

Reads every `coverage.cobertura.xml` produced by
`dotnet test --collect:"XPlat Code Coverage"`, prints a per-project table, and fails when a project
has slipped below the minimum recorded in `build/coverage-floors.json`.

Two details handled here:

* Each report's `filename` values are relative to *that report's* own `<source>` root, and the root
  differs between reports. Joining against the wrong root silently invents projects and misattributes
  files, which makes the whole table quietly wrong rather than obviously broken.
* Generated sources (anything under `obj/`, `*.Designer.cs`, `*.generated.cs`) are excluded. The API
  project alone carries ~1,850 lines of generated OpenAPI support, which would otherwise dominate its
  score and mask the hand-written code the floors exist to protect.
"""
from __future__ import annotations

import argparse
import glob
import json
import os
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

GENERATED_MARKERS = ("/obj/", ".generated.cs", ".g.cs", ".Designer.cs")


def is_generated(path: str) -> bool:
    return any(marker in path for marker in GENERATED_MARKERS)


def collect(results_dir: str, repo_root: str) -> dict[str, dict[int, int]]:
    """Returns {repo-relative file: {line number: hits}}, unioned across every report."""
    files: dict[str, dict[int, int]] = defaultdict(dict)
    reports = glob.glob(os.path.join(results_dir, "**", "coverage.cobertura.xml"), recursive=True)
    if not reports:
        raise SystemExit(f"No coverage reports under {results_dir}. Did the test run collect coverage?")

    for report in reports:
        root = ET.parse(report).getroot()
        sources = [s.text for s in root.iter("source") if s.text]
        base = sources[0] if sources else repo_root
        for cls in root.iter("class"):
            raw = (cls.get("filename") or "").replace("\\", "/")
            if not raw:
                continue
            absolute = raw if raw.startswith("/") else os.path.normpath(os.path.join(base, raw))
            relative = os.path.relpath(absolute, repo_root)
            for line in cls.iter("line"):
                number = int(line.get("number"))
                hits = int(line.get("hits"))
                files[relative][number] = max(files[relative].get(number, 0), hits)
    return files


def project_of(path: str) -> str:
    parts = path.split("/")
    return parts[1] if len(parts) > 1 and parts[0] == "src" else parts[0]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results", default="TestResults/coverage", help="where the reports were written")
    parser.add_argument("--floors", default="build/coverage-floors.json", help="committed minimums")
    parser.add_argument("--repo-root", default=".", help="repository root the reports are relative to")
    parser.add_argument("--update-floors", action="store_true", help="rewrite the floors to today's numbers")
    args = parser.parse_args()

    repo_root = os.path.abspath(args.repo_root)
    files = collect(os.path.abspath(args.results), repo_root)

    totals: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    for path, lines in files.items():
        if path.startswith("test/") or is_generated(path):
            continue
        covered = sum(1 for hits in lines.values() if hits > 0)
        totals[project_of(path)][0] += covered
        totals[project_of(path)][1] += len(lines)

    if not totals:
        raise SystemExit("No hand-written source found in the coverage reports.")

    percentages = {name: 100.0 * c / t for name, (c, t) in totals.items() if t}
    grand_covered = sum(c for c, _ in totals.values())
    grand_total = sum(t for _, t in totals.values())
    grand = 100.0 * grand_covered / grand_total

    floors_path = os.path.join(repo_root, args.floors)
    if args.update_floors:
        payload = {
            "_comment": "Minimum line coverage per project, recorded from a RELEASE run — the "
                        "configuration CI enforces. Release reports far fewer coverable lines than "
                        "Debug (optimized-away sequence points), so its percentages read several "
                        "points higher; regenerating these from a Debug run would leave the ratchet "
                        "slack. Raise a floor as coverage improves; never lower one without saying "
                        "why in the pull request. Regenerate with `make coverage-accept`.",
            "total": round(grand - 0.5, 1),
            "projects": {name: round(value - 0.5, 1) for name, value in sorted(percentages.items())},
        }
        with open(floors_path, "w", encoding="utf-8") as handle:
            json.dump(payload, handle, indent=2)
            handle.write("\n")
        print(f"Wrote {args.floors} from the current run.")
        return 0

    with open(floors_path, encoding="utf-8") as handle:
        floors = json.load(handle)

    print(f"{'PROJECT':<40} {'LINES':>13} {'LINE%':>7} {'FLOOR':>7}  ")
    print("-" * 74)
    failures: list[str] = []
    for name in sorted(percentages, key=lambda key: percentages[key]):
        covered, total = totals[name]
        floor = floors.get("projects", {}).get(name)
        actual = percentages[name]
        marker = ""
        if floor is not None and actual + 1e-9 < floor:
            marker = "  BELOW FLOOR"
            failures.append(f"{name}: {actual:.1f}% is under its {floor:.1f}% floor")
        print(f"{name:<40} {covered:>5}/{total:<7} {actual:>7.1f} {floor if floor is not None else '-':>7}{marker}")

    print("-" * 74)
    total_floor = floors.get("total")
    print(f"{'TOTAL':<40} {grand_covered:>5}/{grand_total:<7} {grand:>7.1f} "
          f"{total_floor if total_floor is not None else '-':>7}")
    if total_floor is not None and grand + 1e-9 < total_floor:
        failures.append(f"total: {grand:.1f}% is under the {total_floor:.1f}% floor")

    # A floor is only enforced for a project that actually reported coverage, so a project whose tests
    # stopped running would drop out of the table and take its floor with it — the run would go green
    # on the very regression the floors exist to catch. Treat a floor with no measurement as a failure.
    for name in sorted(floors.get("projects", {})):
        if name not in percentages:
            failures.append(
                f"{name}: has a floor but produced no coverage — its tests did not run, "
                f"or the project was renamed or removed")

    if failures:
        print()
        for failure in failures:
            print(f"::error::Coverage regressed — {failure}")
        return 1

    print("\nEvery project is at or above its floor.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
