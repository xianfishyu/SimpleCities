"""Summarize raw V4-20 samples without trimming or substituting FPS estimates."""
import json
import math
from pathlib import Path

root = Path(__file__).resolve().parent

def stats(values, budget=None):
    ordered = sorted(values)
    result = {"count": len(ordered), "p95": ordered[math.ceil(len(ordered) * .95) - 1],
              "p99": ordered[math.ceil(len(ordered) * .99) - 1], "max": ordered[-1]}
    if budget is not None:
        result["overBudget"] = sum(x > budget for x in ordered)
    return result

rows = []
for cell in (25, 50, 100, 200):
    data = json.loads((root / f"render-{cell}.json").read_text(encoding="utf-8"))
    assert data["passed"] and not data["smoke"]
    row = {"cell": cell, "fixture": data["fixture"], "frames": {}, "operations": {}}
    for name in ("idle", "panHover", "operationFrames"):
        raw = data[name]
        result = stats(raw["intervalsMs"], 1000 / 144)
        assert abs(result["p95"] - raw["summary"]["p95Ms"]) < 1e-8
        assert abs(result["p99"] - raw["summary"]["p99Ms"]) < 1e-8
        assert result["overBudget"] == raw["summary"]["over144BudgetCount"]
        row["frames"][name] = result
    for name in ("builds", "undos", "upgrades"):
        samples = data[name]
        assert len(samples) == 30
        row["operations"][name] = {key: stats([sample[key] for sample in samples]) for key in
            ("workerQueueMs", "workerMs", "domainMs", "presentationPrepareMs", "resumeMs", "preflightMs", "publicationMs", "referenceCommitMs", "presentationCommitMs", "inputToDrawMs")}
        row["operations"][name]["above100Ms"] = sum(sample["inputToDrawMs"] > 100 for sample in samples)
        row["operations"][name]["above300Ms"] = sum(sample["inputToDrawMs"] > 300 for sample in samples)
    cancellations = data["cancellations"]
    assert len(cancellations) == 10
    row["cancellation"] = {"cancelled": sum(x["cancelled"] for x in cancellations),
        "lostRace": sum(x["lostRace"] for x in cancellations),
        "escapeToIdleMs": stats([x["escapeToIdleMs"] for x in cancellations])}
    rows.append(row)
(root / "summary.json").write_text(json.dumps(rows, indent=2) + "\n", encoding="utf-8")
print(json.dumps(rows, indent=2))
