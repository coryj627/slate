"""W6-2 §F TGF-5 (F6): the §W-G re-grep — over Graph/*.cs and the three WorkspaceViewModel
partials that own graph seams, the signatures of host derivation the
register's pockets name; every hit printed as file:line with the line,
for the record to classify. Run from the repo root."""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC = os.path.join(ROOT, "apps", "slate-windows", "src", "SlateWindows")
FILES = sorted(
    [os.path.join(SRC, "Graph", f) for f in os.listdir(os.path.join(SRC, "Graph")) if f.endswith(".cs")]
    + [os.path.join(SRC, f) for f in ("WorkspaceViewModel.Connections.cs", "WorkspaceViewModel.GraphCreate.cs")]
    + [os.path.join(SRC, "Graph", "WorkspaceViewModel.Graph.cs")]
)
FILES = sorted(set(FILES))

SIGNATURES = [
    ("sorting and comparison", r"\bOrderBy(?:Descending)?\s*\(|\.Sort\s*\(|\bCompareTo\s*\(|\bstring\.Compare\s*\("),
    ("case folding and normalisation", r"\bToLowerInvariant\b|\bToUpperInvariant\b|\bToLower\s*\(|\bToUpper\s*\(|\.Normalize\s*\(|OrdinalIgnoreCase|InvariantCultureIgnoreCase|CurrentCultureIgnoreCase"),
    ("the diameter curve", r"\bMath\.Log\b"),
    ("string composition", r"\bstring\.Format\s*\(|\$\"|\bStringBuilder\b|\bstring\.Join\s*\(|\bstring\.Concat\s*\("),
    ("literals equal to a core constant", r"(?<![\w.])(?:1500|1_500|200|28|8|1|3)(?![\w.])"),
]

def main() -> int:
    out = []
    for path in FILES:
        rel = os.path.relpath(path, ROOT).replace("\\", "/")
        with open(path, encoding="utf-8") as handle:
            lines = handle.read().split("\n")
        for i, line in enumerate(lines, 1):
            stripped = line.strip()
            if stripped.startswith("//") or stripped.startswith("///"):
                continue
            for cls, pattern in SIGNATURES:
                if re.search(pattern, line):
                    out.append((cls, rel, i, stripped[:150]))
    by = {}
    for cls, rel, i, text in out:
        by.setdefault(cls, []).append((rel, i, text))
    for cls, _ in SIGNATURES:
        hits = by.get(cls, [])
        print(f"## {cls}: {len(hits)} hits")
        for rel, i, text in hits:
            print(f"{rel}:{i}: {text}")
        print()
    return 0

if __name__ == "__main__":
    sys.exit(main())
