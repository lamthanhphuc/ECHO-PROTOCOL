import argparse
import os
from pathlib import Path
from xml.etree import ElementTree as ET


def verify_results(results_dir: Path, runner_outcome: str, mode: str = "EditMode") -> str:
    paths = list(results_dir.rglob("*.xml"))
    if not paths:
        raise ValueError(f"No NUnit XML result found in {results_dir}")
    if len(paths) != 1:
        raise ValueError(f"Expected one NUnit XML result, found {len(paths)} in {results_dir}")

    counts = {"total": 0, "passed": 0, "failed": 0, "skipped": 0, "inconclusive": 0}

    for path in paths:
        tree = ET.parse(path)
        root = tree.getroot()
        if root.tag != "test-run":
            raise ValueError(f"Unexpected root tag in {path}")

        values = {key: int(root.attrib[key]) for key in counts}

        if any(value < 0 for value in values.values()):
            raise ValueError(f"Negative NUnit counter in {path}")
        calculated = sum(
            values[key]
            for key in ("passed", "failed", "skipped", "inconclusive")
        )
        if values["total"] != calculated:
            raise ValueError(
                f"Inconsistent NUnit counters in {path}"
            )
        for key, value in values.items():
            counts[key] += value
    if counts["total"] == 0:
        raise ValueError(f"Zero Unity {mode} tests discovered")
    summary = (
        f"Unity {mode}: {counts['passed']} passed, "
        f"{counts['failed']} failed, "
        f"{counts['skipped']} skipped, "
        f"{counts['inconclusive']} inconclusive, "
        f"{counts['total']} total"
    )
    if runner_outcome != "success":
        raise ValueError(f"Unity test runner outcome was {runner_outcome}; {summary}")
    if root.attrib.get("result") != "Passed" or counts["failed"] or counts["inconclusive"]:
        raise ValueError(f"Unity tests not passed; {summary}")
    return summary


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("results_dir", type=Path)
    parser.add_argument(
        "runner_outcome",
        choices=["success", "failure", "cancelled", "skipped"],
    )
    parser.add_argument("mode", nargs="?", choices=["EditMode", "PlayMode"], default="EditMode")
    args = parser.parse_args()
    try:
        summary = verify_results(args.results_dir, args.runner_outcome, args.mode)
        print(summary)
        if os.environ.get("GITHUB_STEP_SUMMARY"):
            with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as output:
                output.write(summary + "\n")
    except (ValueError, ET.ParseError, KeyError) as exc:
        if os.environ.get("GITHUB_STEP_SUMMARY"):
            with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as output:
                output.write(f"Unity {args.mode} failed: {exc}\n")
        parser.exit(1, f"UNITY_TESTS_FAILED: {exc}\n")


if __name__ == "__main__":
    main()
