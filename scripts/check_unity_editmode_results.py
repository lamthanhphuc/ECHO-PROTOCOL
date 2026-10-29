import argparse
from pathlib import Path
from xml.etree import ElementTree as ET


def verify_results(results_dir: Path, runner_outcome: str) -> str:
    if runner_outcome != "success":
        raise ValueError(f"Unity test runner outcome was {runner_outcome}")

    paths = list(results_dir.glob("*.xml"))
    if not paths:
        raise ValueError(f"No NUnit XML result found in {results_dir}")

    counts = {"total": 0, "passed": 0, "failed": 0, "skipped": 0, "inconclusive": 0}

    for path in paths:
        tree = ET.parse(path)
        root = tree.getroot()
        if root.tag != "test-run":
            raise ValueError(f"Unexpected root tag in {path}")

        values = {
            key: int(root.attrib.get(key, 0))
            for key in ("total", "passed", "failed", "skipped", "inconclusive")
        }

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
        if (
            root.attrib.get("result") != "Passed"
            or values["failed"]
            or values["inconclusive"]
        ):
            raise ValueError(f"Unity tests not passed in {path}")
        for key, value in values.items():
            counts[key] += value
    if counts["total"] == 0:
        raise ValueError("Zero Unity EditMode tests discovered")
    return (
        f"Unity EditMode: {counts['passed']} passed, "
        f"{counts['skipped']} skipped, "
        f"{counts['total']} total"
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("results_dir", type=Path)
    parser.add_argument(
        "runner_outcome",
        choices=["success", "failure", "cancelled", "skipped"],
    )
    args = parser.parse_args()
    try:
        print(verify_results(args.results_dir, args.runner_outcome))
    except (ValueError, ET.ParseError) as exc:
        parser.exit(1, f"UNITY_EDITMODE_FAILED: {exc}\n")


if __name__ == "__main__":
    main()
