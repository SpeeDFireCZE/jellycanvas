#!/usr/bin/env python3
"""Add (or replace) one release in manifest.json - the plugin repository
Jellyfin servers read from this GitHub repository.

Called by the Release workflow after the plugin zip is built:

    update-manifest.py --version 1.2.3.0 --zip dist/jellycanvas_1.2.3.0.zip \
        --url https://github.com/.../releases/download/v1.2.3.0/jellycanvas_1.2.3.0.zip

The version's changelog is taken from CHANGELOG.md (the section headed
"## <version>"), the target ABI from build.yaml, and the checksum is the
MD5 of the zip - the form Jellyfin's plugin catalog expects. Versions stay
sorted newest first.
"""
import argparse
import datetime as dt
import hashlib
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent


def target_abi() -> str:
    text = (ROOT / "build.yaml").read_text(encoding="utf-8")
    m = re.search(r'^targetAbi:\s*"?([\d.]+)"?', text, re.M)
    if not m:
        sys.exit("build.yaml: targetAbi not found")
    return m.group(1)


def changelog_for(version: str) -> str:
    """The bullet list under '## <version>' in CHANGELOG.md, as plain text."""
    text = (ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
    m = re.search(rf"^## {re.escape(version)}\b[^\n]*\n(.*?)(?=^## |\Z)", text, re.M | re.S)
    if not m:
        return f"Version {version}."
    body = m.group(1).strip()
    # Sub-headings become plain lines; the catalog shows the text as is.
    body = re.sub(r"^### ", "", body, flags=re.M)
    return body


def version_key(v: str):
    return tuple(int(p) for p in v.split("."))


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--version", required=True)
    ap.add_argument("--zip", required=True, type=pathlib.Path)
    ap.add_argument("--url", required=True)
    args = ap.parse_args()

    if not re.fullmatch(r"\d+\.\d+\.\d+\.\d+", args.version):
        sys.exit("version must have four parts, e.g. 1.2.3.0")

    checksum = hashlib.md5(args.zip.read_bytes()).hexdigest()  # noqa: S324 - the catalog format wants MD5
    manifest_path = ROOT / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    plugin = manifest[0]

    entry = {
        "version": args.version,
        "changelog": changelog_for(args.version),
        "targetAbi": target_abi(),
        "sourceUrl": args.url,
        "checksum": checksum,
        "timestamp": dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    versions = [v for v in plugin["versions"] if v["version"] != args.version]
    versions.append(entry)
    versions.sort(key=lambda v: version_key(v["version"]), reverse=True)
    plugin["versions"] = versions

    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"manifest.json: {args.version} ({checksum})")


if __name__ == "__main__":
    main()
