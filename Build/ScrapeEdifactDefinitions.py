#!/usr/bin/env python3
"""
Scrapes EDIFACT directory definitions from edifactory.de and writes
  PathFinder/Resources/EdifactDefinitions.json   (plain JSON, ~5-15 MB)
  PathFinder/Resources/EdifactDefinitions.json.gz (compressed, ~500 KB-2 MB)

These files are embedded in the PathFinder assembly as resources.

Usage:
    python Build/ScrapeEdifactDefinitions.py [--no-cache] [--dirs D96A,D97A]

Requirements:
    pip install requests beautifulsoup4

The scraper caches every fetched page to Build/.scraper_cache/ so re-runs
are fast.  Use --no-cache to force a fresh fetch of everything.

Output JSON schema:
  {
    "version": "1.0",
    "generatedAt": "...",
    "directories": {
      "D96A": {
        "messages": {
          "IFTMCS": {
            "structure": [ <EdifactStructureItem>... ]
          }
        },
        "segments": {
          "BGM": {
            "tag": "BGM",
            "fields": [ <EdifactFieldDef>... ]
          }
        },
        "codeLists": {
          "1001": {"1":"Certificate of analysis","2":"Certificate of conformity",...}
        }
      }
    },
    "serviceSegments": {
      "UNB": { "tag": "UNB", "fields": [...] },
      ...
    }
  }

EdifactStructureItem:
  { "kind": "segment", "tag": "BGM", "mandatory": true,  "maxOccurrences": 1 }
  { "kind": "group",   "name": "SG1","mandatory": false, "maxOccurrences": 99,
    "items": [ <EdifactStructureItem>... ] }

EdifactFieldDef:
  {
    "id": "1004", "name": "DOCUMENT/MESSAGE NUMBER",
    "mandatory": false, "isComposite": false,
    "dataType": "an", "maxLength": 35, "isLink": false
  }
  {
    "id": "C002", "name": "DOCUMENT/MESSAGE NAME",
    "mandatory": false, "isComposite": true,
    "components": [ <EdifactFieldDef>... ]
  }
"""

from __future__ import annotations

import argparse
import gzip
import json
import re
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import requests
from bs4 import BeautifulSoup

# ── Configuration ────────────────────────────────────────────────────────────────

BASE_URL        = "https://www.edifactory.de"
DEFAULT_DIRS    = ["D96A", "D97A", "D98A", "D99A", "D99B", "D01B", "D10B"]
SERVICE_SEGMENT_TAGS = ["UNB", "UNG", "UNE", "UNH", "UNT", "UNZ"]  # UNA handled separately
RATE_LIMIT_SEC  = 0.5  # polite delay between HTTP requests
CACHE_DIR       = Path("Build/.scraper_cache")
OUTPUT_JSON     = Path("PathFinder/Resources/EdifactDefinitions.json")
OUTPUT_GZ       = Path("PathFinder/Resources/EdifactDefinitions.json.gz")
HEADERS         = {"User-Agent": "PathFinder-Scraper/1.0 (build-tool, read-only)"}

# ── HTTP with local disk cache ────────────────────────────────────────────────────

_last_req_time: float = 0.0


def _cache_path(url: str) -> Path:
    key = re.sub(r"[^A-Za-z0-9._-]", "_", url.replace(BASE_URL, "")) + ".html"
    return CACHE_DIR / key


def fetch(url: str, use_cache: bool = True) -> str:
    global _last_req_time

    cp = _cache_path(url)
    if use_cache and cp.exists():
        return cp.read_text(encoding="utf-8")

    wait = RATE_LIMIT_SEC - (time.time() - _last_req_time)
    if wait > 0:
        time.sleep(wait)

    resp = requests.get(url, headers=HEADERS, timeout=30)
    resp.raise_for_status()
    html = resp.text
    _last_req_time = time.time()

    if use_cache:
        CACHE_DIR.mkdir(parents=True, exist_ok=True)
        cp.write_text(html, encoding="utf-8")

    return html


# ── Messages list ──────────────────────────────────────────────────────────────────

def get_message_types(directory: str, use_cache: bool) -> list[str]:
    """Return all message type codes in a directory (all in the HTML - client-side DataTables)."""
    url  = f"{BASE_URL}/edifact/directory/{directory}/messages"
    html = fetch(url, use_cache)
    soup = BeautifulSoup(html, "html.parser")

    pattern = re.compile(rf"/edifact/directory/{directory}/message/([A-Z]+)$")
    seen: set[str] = set()
    result: list[str] = []
    for a in soup.find_all("a", href=True):
        m = pattern.match(a["href"])
        if m:
            code = m.group(1)
            if code not in seen:
                seen.add(code)
                result.append(code)
    return result


# ── Message structure ──────────────────────────────────────────────────────────────

# Matches:  UNH, Message header                           M        1
_SEG_LINE   = re.compile(r"^([A-Z]{2,4}),\s+.+?\s{2,}(M|C)\s+(\d+)")
# Matches:  --- Segment Group 1 -------- C       99 -------+
#       or  --- Segment Group 5 -------- C        9 ------+|
_GROUP_LINE = re.compile(
    r"^---\s+Segment Group\s+(\d+)\s+-+\s+(M|C)\s+(\d+)\s+-+\+([|]*)"
)
# Matches group-close lines that start with ---- and contain + but not "Segment Group"
_CLOSE_LINE = re.compile(r"^-{4,}")


def _parse_structure_pre(pre_text: str) -> list:
    """
    Parse the fixed-width pre-formatted message structure text into a
    nested list of EdifactStructureItem dicts.

    Depth rules:
      - Root-level segments:       no trailing '|'
      - Inside SG1:                trailing '|'   (depth 1)
      - Inside SG5 (in SG4):      trailing '||'  (depth 2)
      - Group header trailing '+': opens 1 new level
        trailing '+|': opens 1 level while already at depth 1
      - Group close trailing '+':  closes 1 level
        trailing '++': closes 2 levels, etc.
    """
    root: list = []
    stack: list[list] = [root]

    for raw_line in pre_text.split("\n"):
        line = raw_line.rstrip()
        stripped = line.strip()
        if not stripped:
            continue

        # ── Group close  (---- ... + or ++)
        if _CLOSE_LINE.match(stripped) and "+" in stripped and "Segment Group" not in stripped:
            n_close = stripped.count("+")
            for _ in range(min(n_close, len(stack) - 1)):
                stack.pop()
            continue

        # ── Group header  (--- Segment Group N ...)
        m = _GROUP_LINE.match(stripped)
        if m:
            parent_depth = len(m.group(4))  # pipes after the final '+' = parent depth
            target_len   = parent_depth + 1  # expected stack size at parent level
            while len(stack) > target_len:
                stack.pop()
            group: dict = {
                "kind":           "group",
                "name":           f"SG{m.group(1)}",
                "mandatory":      m.group(2) == "M",
                "maxOccurrences": int(m.group(3)),
                "items":          [],
            }
            stack[-1].append(group)
            stack.append(group["items"])
            continue

        # ── Segment line  (TAG, Description   M/C   count)
        m = _SEG_LINE.match(stripped)
        if m:
            stack[-1].append({
                "kind":           "segment",
                "tag":            m.group(1),
                "mandatory":      m.group(2) == "M",
                "maxOccurrences": int(m.group(3)),
            })

    return root


def get_message_structure(directory: str, msg_type: str, use_cache: bool) -> list:
    url  = f"{BASE_URL}/edifact/directory/{directory}/message/{msg_type}"
    html = fetch(url, use_cache)
    soup = BeautifulSoup(html, "html.parser")

    header = next(
        (e for e in soup.find_all("h3") if "Message structure" in e.get_text()),
        None
    )
    if not header:
        return []

    pre = next(
        (s for s in header.next_siblings if getattr(s, "name", None) == "pre"),
        None
    )
    if not pre:
        return []

    return _parse_structure_pre(pre.get_text())


def collect_segment_tags_from_structure(structure: list) -> set[str]:
    tags: set[str] = set()
    for item in structure:
        if item.get("kind") == "segment":
            tags.add(item["tag"])
        elif item.get("kind") == "group":
            tags.update(collect_segment_tags_from_structure(item["items"]))
    return tags


# ── Segment field definitions ──────────────────────────────────────────────────────

_TYPE_SPEC = re.compile(r"(an?|n|a)(\d+)(?:\.\.(\d+))?")

# Field line patterns (all anchored at start of *stripped* line)
_COMP_LINE  = re.compile(r"^\d{4}\s+.+?\s{2,}(M|C)(?:\s+\S+)?$")          # component (will be matched after indent check)
_CMPX_HDR   = re.compile(r"^([CS]\d{3})\s+(.+?)\s{2,}(M|C)")               # composite/service composite header
_SIMP_LINE  = re.compile(r"^(\d{4})\s+(.+?)\s{2,}(M|C)(?:\s+(\S+))?")     # simple field


def _parse_type_spec(spec: str | None) -> tuple[str, int]:
    if not spec:
        return ("an", 0)
    m = _TYPE_SPEC.search(spec)
    if not m:
        return ("an", 0)
    data_type = m.group(1)
    max_len   = int(m.group(3)) if m.group(3) else int(m.group(2))
    return (data_type, max_len)


def get_segment_def(directory: str, tag: str, use_cache: bool) -> list:
    """Return a list of EdifactFieldDef dicts for the given segment tag."""
    url = f"{BASE_URL}/edifact/directory/{directory}/segment/{tag}/popup"
    try:
        html = fetch(url, use_cache)
    except requests.HTTPError:
        return []

    soup = BeautifulSoup(html, "html.parser")

    header = next(
        (e for e in soup.find_all("h3") if "Segment structure" in e.get_text()),
        None
    )
    if not header:
        return []

    pre = next(
        (s for s in header.next_siblings if getattr(s, "name", None) == "pre"),
        None
    )
    if not pre:
        return []

    # Collect element IDs that appear as links (they have code lists)
    linked_ids: set[str] = set()
    for a in pre.find_all("a", href=True):
        m = re.search(r"/data-element/(\w+)$", a["href"])
        if m:
            linked_ids.add(m.group(1))

    text             = pre.get_text()
    fields: list     = []
    current_comp     = None   # currently open composite field

    for raw_line in text.split("\n"):
        raw  = raw_line.rstrip()
        stripped = raw.strip()
        if not stripped:
            continue

        # ── Component line: indented 3+ spaces + 4-digit ID
        if re.match(r"^\s{3,}\d{4}", raw):
            m = re.match(r"^\s+(\d{4})\s+(.+?)\s{2,}(M|C)(?:\s+(\S+))?", raw)
            if m and current_comp is not None:
                dtype, mlen = _parse_type_spec(m.group(4))
                comp = {
                    "id":        m.group(1),
                    "name":      m.group(2).strip(),
                    "mandatory": m.group(3) == "M",
                    "dataType":  dtype,
                    "maxLength": mlen,
                    "isLink":    m.group(1) in linked_ids,
                }
                current_comp["components"].append(comp)
            continue

        # ── Composite/service-composite header: C003 or S009
        m = _CMPX_HDR.match(stripped)
        if m:
            current_comp = {
                "id":          m.group(1),
                "name":        m.group(2).strip(),
                "mandatory":   m.group(3) == "M",
                "isComposite": True,
                "components":  [],
            }
            fields.append(current_comp)
            continue

        # ── Simple field: 4-digit ID (flush left)
        m = _SIMP_LINE.match(stripped)
        if m and not raw.startswith(" "):
            current_comp = None
            dtype, mlen  = _parse_type_spec(m.group(4))
            fields.append({
                "id":          m.group(1),
                "name":        m.group(2).strip(),
                "mandatory":   m.group(3) == "M",
                "isComposite": False,
                "dataType":    dtype,
                "maxLength":   mlen,
                "isLink":      m.group(1) in linked_ids,
            })

    return fields


def collect_linked_element_ids(fields: list) -> set[str]:
    ids: set[str] = set()
    for f in fields:
        if f.get("isLink"):
            ids.add(f["id"])
        for comp in f.get("components") or []:
            if comp.get("isLink"):
                ids.add(comp["id"])
    return ids


# ── Code lists ──────────────────────────────────────────────────────────────────────

def get_code_list(directory: str, element_id: str, use_cache: bool) -> dict[str, str]:
    """
    Return all valid code values (with descriptions) for a data element.
    All rows are present in the initial HTML (client-side DataTables).
    Returns a dict mapping code → description.
    """
    url = f"{BASE_URL}/edifact/directory/{directory}/data-element/{element_id}"
    try:
        html = fetch(url, use_cache)
    except requests.HTTPError:
        return {}

    soup  = BeautifulSoup(html, "html.parser")
    table = soup.find("table")
    if not table:
        return {}

    codes: dict[str, str] = {}
    for row in table.find_all("tr")[1:]:   # skip header
        cells = row.find_all("td")
        if cells:
            code = cells[0].get_text(strip=True)
            desc = cells[1].get_text(strip=True) if len(cells) > 1 else ""
            if code:
                codes[code] = desc
    return codes


# ── Directory scraping ─────────────────────────────────────────────────────────────

def scrape_directory(directory: str, use_cache: bool) -> dict:
    """Scrape one complete directory and return its definition dict."""

    # ── Step 1: get all message types ───────────────────────────────────────────
    print(f"  [{directory}] Getting message list …", flush=True)
    msg_types = get_message_types(directory, use_cache)
    print(f"  [{directory}] {len(msg_types)} messages found", flush=True)

    # ── Step 2: scrape message structures ───────────────────────────────────────
    messages:          dict[str, dict]  = {}
    all_segment_tags:  set[str]        = set()

    for i, mt in enumerate(msg_types, 1):
        print(f"  [{directory}] structure {i}/{len(msg_types)}: {mt}   ", end="\r", flush=True)
        structure = get_message_structure(directory, mt, use_cache)
        messages[mt] = {"structure": structure}
        all_segment_tags.update(collect_segment_tags_from_structure(structure))

    print(f"\n  [{directory}] {len(all_segment_tags)} unique segment tags", flush=True)

    # ── Step 3: scrape segment field definitions ─────────────────────────────────
    segments:              dict[str, dict]  = {}
    all_linked_element_ids: set[str]        = set()

    for i, tag in enumerate(sorted(all_segment_tags), 1):
        print(f"  [{directory}] segment {i}/{len(all_segment_tags)}: {tag}   ", end="\r", flush=True)
        fields = get_segment_def(directory, tag, use_cache)
        if fields:
            segments[tag] = {"tag": tag, "fields": fields}
            all_linked_element_ids.update(collect_linked_element_ids(fields))

    print(f"\n  [{directory}] {len(all_linked_element_ids)} coded element IDs", flush=True)

    # ── Step 4: scrape code lists ───────────────────────────────────────────────
    code_lists: dict[str, dict[str, str]] = {}

    for i, eid in enumerate(sorted(all_linked_element_ids), 1):
        print(f"  [{directory}] code list {i}/{len(all_linked_element_ids)}: {eid}   ", end="\r", flush=True)
        codes = get_code_list(directory, eid, use_cache)
        if codes:
            code_lists[eid] = codes

    print(
        f"\n  [{directory}] done — "
        f"{len(messages)} msgs, {len(segments)} segs, {len(code_lists)} code lists",
        flush=True,
    )

    return {
        "messages":  messages,
        "segments":  segments,
        "codeLists": code_lists,
    }


# ── Service segments (UNA handled specially; rest via D96A) ────────────────────────

def scrape_service_segments(use_cache: bool) -> dict[str, dict]:
    print("  Scraping ISO9735 service segments …", flush=True)
    host = "D96A"
    result: dict[str, dict] = {}

    # UNA has a fixed 9-character format (not a normal structured segment)
    result["UNA"] = {
        "tag": "UNA",
        "fields": [
            {
                "id": "UNA_CHARS",
                "name": "SERVICE STRING ADVICE (6 delimiter characters)",
                "mandatory": False,
                "isComposite": False,
                "dataType": "an",
                "maxLength": 6,
                "isLink": False,
            }
        ],
    }

    for tag in SERVICE_SEGMENT_TAGS:
        print(f"    {tag}", flush=True)
        fields = get_segment_def(host, tag, use_cache)
        if fields:
            result[tag] = {"tag": tag, "fields": fields}

    return result


# ── Entry point ────────────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Scrape EDIFACT definitions and write PathFinder embedded resource"
    )
    parser.add_argument(
        "--no-cache",
        action="store_true",
        help="Bypass disk cache and re-fetch all pages",
    )
    parser.add_argument(
        "--dirs",
        default=",".join(DEFAULT_DIRS),
        help=f"Comma-separated directories to scrape (default: {','.join(DEFAULT_DIRS)})",
    )
    args = parser.parse_args()

    use_cache    = not args.no_cache
    target_dirs  = [d.strip().upper() for d in args.dirs.split(",") if d.strip()]

    print(f"EDIFACT Definition Scraper")
    print(f"  Directories : {', '.join(target_dirs)}")
    print(f"  Cache       : {'enabled' if use_cache else 'disabled (--no-cache)'}")
    print(f"  Output      : {OUTPUT_JSON}", flush=True)

    all_data: dict = {
        "version":         "1.0",
        "generatedAt":     datetime.now(timezone.utc).isoformat(),
        "directories":     {},
        "serviceSegments": {},
    }

    all_data["serviceSegments"] = scrape_service_segments(use_cache)

    for directory in target_dirs:
        print(f"\nDirectory: {directory}", flush=True)
        all_data["directories"][directory] = scrape_directory(directory, use_cache)

    # ── Write outputs ────────────────────────────────────────────────────────────
    OUTPUT_JSON.parent.mkdir(parents=True, exist_ok=True)

    json_text = json.dumps(all_data, ensure_ascii=False, separators=(",", ":"))
    OUTPUT_JSON.write_text(json_text, encoding="utf-8")
    print(f"\nWrote {OUTPUT_JSON}  ({len(json_text):,} bytes)")

    with gzip.open(OUTPUT_GZ, "wt", encoding="utf-8", compresslevel=9) as gz:
        gz.write(json_text)
    print(f"Wrote {OUTPUT_GZ}  ({OUTPUT_GZ.stat().st_size:,} bytes)")

    print("Done.")


if __name__ == "__main__":
    main()
