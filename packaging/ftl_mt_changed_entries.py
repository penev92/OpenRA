#!/usr/bin/env python3
import argparse
import json
import os
import re
import subprocess
import sys
from pathlib import Path
from typing import Dict, Tuple

from fluent.syntax.parser import FluentParser
from fluent.syntax import ast


PLACEABLE_RE = re.compile(r"\{[^}]*\}")


def sh(cmd, check=True) -> subprocess.CompletedProcess:
    return subprocess.run(cmd, check=check, text=True, capture_output=True)


def git_show(ref: str, path: str) -> str:
    """Return file content at ref:path or '' if missing."""
    if not ref or set(ref) == {"0"}:
        return ""
    r = sh(["git", "show", f"{ref}:{path}"], check=False)
    return r.stdout if r.returncode == 0 else ""


def changed_ftl_files(base: str, head: str) -> list[str]:
    """List changed *.ftl files between base and head."""
    if not base or set(base) == {"0"}:
        r = sh(["git", "ls-files", "*.ftl"], check=False)
        return [p for p in r.stdout.splitlines() if p.strip()]

    r = sh(["git", "diff", "--name-only", base, head, "--", "*.ftl"], check=False)
    return [p for p in r.stdout.splitlines() if p.strip()]


def parse_entries_with_spans(ftl_text: str) -> Dict[str, str]:
    """
    Map entry-id -> raw entry text.
    Includes:
      - Messages: "foo"
      - Terms: "-bar"
    Uses AST spans to slice the original text (keeps formatting/indentation).
    """
    out: Dict[str, str] = {}
    if not ftl_text.strip():
        return out

    parser = FluentParser(with_spans=True)
    res = parser.parse(ftl_text)

    for node in res.body:
        if isinstance(node, ast.Message) and node.id and getattr(node, "span", None):
            key = node.id.name
            out[key] = ftl_text[node.span.start : node.span.end].rstrip()
        elif isinstance(node, ast.Term) and node.id and getattr(node, "span", None):
            key = f"-{node.id.name}"
            out[key] = ftl_text[node.span.start : node.span.end].rstrip()

    return out


def extract_placeables(s: str) -> list[str]:
    return PLACEABLE_RE.findall(s)


def cmd_extract(base: str, head: str, target_lang: str, out_path: str) -> int:
    files = changed_ftl_files(base, head)

    items = []
    for path in files:
        base_text = git_show(base, path)

        head_file = Path(path)
        if head_file.exists():
            head_text = head_file.read_text("utf-8")
        else:
            # file deleted in head -> ignore (no translations to produce)
            continue

        base_map = parse_entries_with_spans(base_text) if base_text else {}
        head_map = parse_entries_with_spans(head_text)

        added = sorted(set(head_map) - set(base_map))
        modified = sorted(k for k in set(head_map) & set(base_map) if head_map[k] != base_map[k])

        for k in added:
            items.append(
                {
                    "file": path,
                    "id": k,
                    "status": "ADDED",
                    "entry": head_map[k],
                    "placeables": extract_placeables(head_map[k]),
                }
            )

        for k in modified:
            items.append(
                {
                    "file": path,
                    "id": k,
                    "status": "MODIFIED",
                    "entry": head_map[k],
                    "placeables": extract_placeables(head_map[k]),
                }
            )

    payload = {
        "schema": "ftl-mt-request-v1",
        "target_lang": target_lang,
        "items": items,
    }

    Path(out_path).write_text(json.dumps(payload, ensure_ascii=False, indent=2), "utf-8")
    print(out_path)
    return 0


def cmd_print(response_file: str) -> int:
    data = json.loads(Path(response_file).read_text("utf-8"))

    items = data.get("items", [])
    if not items:
        print("No translated items.")
        return 0

    for it in items:
        file = it.get("file")
        _id = it.get("id")
        status = it.get("status")
        translated = it.get("translated_entry", "").rstrip()

        # Basic placeholder safety check (optional but useful)
        expected = it.get("placeables", [])
        got = extract_placeables(translated)
        ok = (expected == got)

        print(f"=== {file} :: {status} :: {_id} ===")
        if not ok:
            print("WARNING: placeables changed!")
            print(f"  expected: {expected}")
            print(f"  got:      {got}")
        print(translated)
        print()

    return 0


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)

    ap_ex = sub.add_parser("extract")
    ap_ex.add_argument("--base", required=True)
    ap_ex.add_argument("--head", required=True)
    ap_ex.add_argument("--lang", required=True, help="Target language code (e.g. bg, de, fr)")
    ap_ex.add_argument("--out", default="mt_request.json")

    ap_pr = sub.add_parser("print")
    ap_pr.add_argument("--response", required=True, help="Path to ai-inference response JSON file")

    args = ap.parse_args()

    if args.cmd == "extract":
        return cmd_extract(args.base, args.head, args.lang, args.out)
    if args.cmd == "print":
        return cmd_print(args.response)

    return 2


if __name__ == "__main__":
    raise SystemExit(main())
