#!/usr/bin/env python3
import json
import os
import subprocess
from pathlib import Path

from fluent.syntax.parser import FluentParser
from fluent.syntax import ast


def sh(cmd: list[str]) -> subprocess.CompletedProcess:
    return subprocess.run(cmd, check=False, text=True, capture_output=True)


def git_show(ref: str, path: str) -> str:
    """Return file content at ref:path or '' if missing."""
    if not ref or set(ref) == {"0"}:
        return ""
    r = sh(["git", "show", f"{ref}:{path}"])
    return r.stdout if r.returncode == 0 else ""


def changed_ftl_files(base: str, head: str) -> list[str]:
    """
    List changed *.ftl files between base and head.
    Handles first push (base is all-zero SHA) by listing current *.ftl files.
    """
    if not base or set(base) == {"0"}:
        r = sh(["git", "ls-files", "*.ftl"])
        return [p for p in r.stdout.splitlines() if p.strip()]

    r = sh(["git", "diff", "--name-only", base, head, "--", "*.ftl"])
    return [p for p in r.stdout.splitlines() if p.strip()]


def parse_entries(ftl_text: str) -> dict[str, str]:
    """
    Return entry-id -> raw entry block text (span-sliced).
    Includes Messages ("foo") and Terms ("-bar").
    """
    out: dict[str, str] = {}
    if not ftl_text.strip():
        return out

    parser = FluentParser(with_spans=True)
    res = parser.parse(ftl_text)

    for node in res.body:
        if isinstance(node, ast.Message) and node.id and getattr(node, "span", None):
            out[node.id.name] = ftl_text[node.span.start : node.span.end].rstrip()
        elif isinstance(node, ast.Term) and node.id and getattr(node, "span", None):
            out[f"-{node.id.name}"] = ftl_text[node.span.start : node.span.end].rstrip()

    return out


def chunk_items(items: list[dict], max_chars: int = 24000) -> list[list[dict]]:
    """
    Chunk by approximate JSON size (characters).
    Keeps each chunk safely under token/request limits for free tiers.
    """
    chunks: list[list[dict]] = []
    cur: list[dict] = []
    cur_len = 0

    for it in items:
        s = json.dumps(it, ensure_ascii=False)
        if cur and cur_len + len(s) > max_chars:
            chunks.append(cur)
            cur, cur_len = [], 0
        cur.append(it)
        cur_len += len(s)

    if cur:
        chunks.append(cur)

    return chunks


def main() -> None:
    base = os.environ.get("BASE_SHA", "")
    head = os.environ.get("HEAD_SHA", "")

    files = changed_ftl_files(base, head)

    items: list[dict] = []

    for path in files:
        base_text = git_show(base, path)

        head_path = Path(path)
        if not head_path.exists():
            # File deleted in head -> nothing to translate
            continue
        head_text = head_path.read_text("utf-8")

        base_map = parse_entries(base_text) if base_text else {}
        head_map = parse_entries(head_text)

        added = sorted(set(head_map) - set(base_map))
        modified = sorted(k for k in (set(head_map) & set(base_map)) if head_map[k] != base_map[k])

        for k in added:
            items.append({"file": path, "id": k, "status": "ADDED", "entry": head_map[k]})
        for k in modified:
            items.append({"file": path, "id": k, "status": "MODIFIED", "entry": head_map[k]})

    out_dir = Path("mt_chunks")
    out_dir.mkdir(exist_ok=True)

    chunks = chunk_items(items, max_chars=24000)
    manifest: list[str] = []

    for i, ch in enumerate(chunks, start=1):
        name = f"mt_request_{i:03d}.json"
        payload = {"schema": "ftl-mt-request-v1", "items": ch}
        (out_dir / name).write_text(json.dumps(payload, ensure_ascii=False, indent=2), "utf-8")
        manifest.append(str(out_dir / name))

    Path("chunks.json").write_text(json.dumps(manifest, indent=2), "utf-8")

    # Convenient for GH Actions: print a "chunks=" line you can capture as output
    print(f"chunks={json.dumps(manifest)}")


if __name__ == "__main__":
    main()
