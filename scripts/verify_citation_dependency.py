#!/usr/bin/env python3
"""Admit the audited temporary Git patch and verify the actual application graph."""

import argparse
import json
from pathlib import Path
import subprocess
import tomllib


GIT_URL = "https://github.com/typst/citationberg"
GIT_REV = "06a591e2f237d25e1dfdedac3f3d1494c496c52d"
GIT_SOURCE = f"git+{GIT_URL}?rev={GIT_REV}#{GIT_REV}"
REGISTRY = "registry+https://github.com/rust-lang/crates.io-index"
XML_CHECKSUM = "e660451e55124f798a69a5af3f49ccfbefbd41910eefd25caf2393e1f3473ec1"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def one(packages, name):
    matches = [p for p in packages if p["name"] == name]
    require(len(matches) == 1, f"expected exactly one {name} package")
    return matches[0]


def verify(manifest, lock, metadata):
    require(manifest.get("patch", {}).get("crates-io", {}).get("citationberg")
            == {"git": GIT_URL, "rev": GIT_REV}, "citationberg manifest pin changed")
    packages = metadata["packages"]
    require(metadata["version"] == 1, "unsupported Cargo metadata format")
    for label, items in (("lock", lock["package"]), ("resolved graph", packages)):
        git = [p for p in items if (p.get("source") or "").startswith("git+")]
        require(len(git) == 1 and git[0]["name"] == "citationberg"
                and git[0]["version"] == "0.7.0" and git[0]["source"] == GIT_SOURCE,
                f"{label}: unapproved Git package/source/revision")
        xml = one(items, "quick-xml")
        require(xml["version"] == "0.41.0" and xml.get("source") == REGISTRY,
                f"{label}: unapproved quick-xml resolution")
    require(one(lock["package"], "quick-xml").get("checksum") == XML_CHECKSUM,
            "quick-xml registry checksum changed")
    citation = one(packages, "citationberg")
    require(citation["source"] == GIT_SOURCE and citation["version"] == "0.7.0",
            "resolved citationberg differs from approved lock")
    xml = one(packages, "quick-xml")
    package_ids = {p["id"] for p in packages}
    nodes = metadata["resolve"]["nodes"]
    require(len(package_ids) == len(packages), "duplicate resolved package IDs")
    graph = {n["id"]: n for n in nodes}
    require(len(graph) == len(nodes) and set(graph) == package_ids,
            "incomplete or duplicate resolved nodes")
    for node in nodes:
        require(all(d["pkg"] in graph for d in node["deps"]),
                "resolved edge names an absent package")

    def normal_path(start, target):
        pending = [(start, [start])]
        seen = set()
        while pending:
            current, path = pending.pop()
            if current == target:
                return path
            if current in seen:
                continue
            seen.add(current)
            for dep in graph[current]["deps"]:
                if any(kind["kind"] is None for kind in dep["dep_kinds"]):
                    pending.append((dep["pkg"], path + [dep["pkg"]]))
        raise ValueError(f"no normal dependency path from {start} to {target}")

    paths = {}
    for name in ("slate-uniffi", "slate-cli"):
        app = one(packages, name)
        require(app.get("source") is None and app["id"] in metadata["workspace_members"],
                f"{name} is not the workspace application")
        paths[name] = {
            "citationberg": normal_path(app["id"], citation["id"]),
            "quick-xml": normal_path(app["id"], xml["id"]),
        }
    normal_path(citation["id"], xml["id"])
    return {"approvedGitSource": GIT_SOURCE, "quickXmlVersion": xml["version"],
            "quickXmlRegistryChecksum": XML_CHECKSUM, "applicationPaths": paths}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--offline", action="store_true", help="require already fetched dependencies")
    args = parser.parse_args()
    root = args.root.resolve()
    manifest_path, lock_path = root / "Cargo.toml", root / "Cargo.lock"
    manifest_bytes, lock_bytes = manifest_path.read_bytes(), lock_path.read_bytes()
    command = ["cargo", "metadata", "--locked", "--format-version", "1", "--all-features"]
    if args.offline:
        command.append("--offline")
    try:
        metadata = json.loads(subprocess.run(command, cwd=root, check=True,
                                            capture_output=True, text=True).stdout)
        result = verify(tomllib.loads(manifest_bytes.decode()),
                        tomllib.loads(lock_bytes.decode()), metadata)
        citation = one(metadata["packages"], "citationberg")
        checkout = Path(citation["manifest_path"]).parent
        revision = subprocess.run(["git", "-C", str(checkout), "rev-parse", "HEAD"],
                                  check=True, capture_output=True, text=True).stdout.strip()
        dirty = subprocess.run(["git", "-C", str(checkout), "status", "--porcelain",
                                "--untracked-files=no"], check=True,
                               capture_output=True, text=True).stdout
        require(revision == GIT_REV and not dirty,
                "fetched citationberg checkout is not clean at the admitted revision")
        result["fetchedGitHead"] = revision
        result["fetchedTrackedFilesClean"] = True
        result["offline"] = args.offline
        print(json.dumps(result, indent=2))
    finally:
        require(manifest_path.read_bytes() == manifest_bytes and lock_path.read_bytes() == lock_bytes,
                "dependency verification changed the manifest or lockfile")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, TypeError, OSError, subprocess.CalledProcessError) as error:
        raise SystemExit(f"citation dependency policy failed: {error}") from error
