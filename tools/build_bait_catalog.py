"""Build the bundled fish/bait snapshot from GatherBuddy Reborn's Fish/Data*.cs files.

Usage: python tools/build_bait_catalog.py PATH_TO_GATHERBUDDYREBORN
"""

import json
import re
import sys
from pathlib import Path


def call_arguments(chain: str, method: str) -> list[str]:
    calls: list[str] = []
    for match in re.finditer(r"\." + method + r"\s*\(", chain):
        start = match.end()
        depth = 1
        for pos in range(start, len(chain)):
            if chain[pos] == "(":
                depth += 1
            elif chain[pos] == ")":
                depth -= 1
                if depth == 0:
                    calls.append(chain[start:pos])
                    break
        else:
            raise ValueError(f"Unclosed {method} call")
    return calls


def main() -> None:
    upstream = Path(sys.argv[1]) / "GatherBuddy.GameData" / "Data" / "Fish"
    output = Path(__file__).resolve().parents[1] / "BaitBeacon" / "Data" / "fish-bait.json"
    recipes: dict[int, dict[str, object]] = {}
    apply_count = 0
    for path in sorted(upstream.glob("Data[0-9]*.cs")):
        source = re.sub(r"//[^\n]*", "", path.read_text(encoding="utf-8-sig"))
        for match in re.finditer(r"\bdata\.Apply\s*\(\s*(\d+)\s*,", source):
            apply_count += 1
            fish_id = int(match.group(1))
            end = source.find(";", match.end())
            if end < 0:
                raise ValueError(f"Missing semicolon after fish {fish_id} in {path}")
            chain = source[match.end() : end]
            bait = re.findall(r"\.Bait\s*\(\s*data\s*,\s*(\d+)\s*\)", chain)
            initial_bait = int(bait[-1]) if bait else 0
            mooch: list[int] = []
            for call in call_arguments(chain, "Mooch"):
                values = [int(value) for value in re.findall(r"\d+", call)]
                if len(values) > 1:
                    # The multi-argument overload starts with initial bait,
                    # followed by one or more mooch fish IDs.
                    initial_bait = values[0]
                    mooch.extend(values[1:])
                else:
                    mooch.extend(values)
            predators: list[int] = []
            for call in call_arguments(chain, "Predators"):
                predators.extend(int(value) for value in re.findall(r"\(\s*(\d+)\s*,", call))
            recipes[fish_id] = {
                "b": initial_bait,
                "m": sorted(set(mooch)),
                "p": sorted(set(predators)),
            }
    if apply_count < 2000 or len(recipes) < 2000:
        raise ValueError(f"Incomplete upstream data: {apply_count} Apply calls, {len(recipes)} fish")
    output.write_text(json.dumps(dict(sorted(recipes.items())), separators=(",", ":")) + "\n", encoding="utf-8")
    print(f"Wrote {len(recipes)} fish recipes from {apply_count} Apply calls to {output}")


if __name__ == "__main__":
    main()
