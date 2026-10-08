#!/usr/bin/env python3
"""Generate docs/data/packets.json for Minecraft 26.3 / protocol 777 from the
Minecraft Wiki "Java Edition protocol/Packets" page artifacts.

Source artifacts (fetched 2026):
  _packets.raw.html      rendered page (authoritative packet ID index)
  _packets.raw.wikitext.txt  page wikitext (field definitions)

This script is a one-shot generator; docs/data/packets.json is the committed artifact.
"""
import re
import json
import sys
import html as htmlmod

sys.stdout.reconfigure(encoding="utf-8")

BASE = r"C:\Git\VoXelSharp\docs\data"
RAW_HTML = BASE + r"\_raw\_packets.raw.html"
RAW_WIKI = BASE + r"\_raw\_packets.raw.wikitext.txt"

STATE_ORDER = ["handshaking", "status", "login", "configuration", "play"]

def strip_type_templates(text):
    """Convert {{Type|X}} to X; keep textual suffixes like (255)."""
    text = text.replace("{{Type|", "{{TYPE|").replace("{{type|", "{{TYPE|")
    text = re.sub(r"\{\{TYPE\|([^}|]+)(?:\|[^}]*)?\}\}", r"\1", text)
    # resolve [[link|label]] and [[label]] in type strings
    text = re.sub(r"\[\[[^\]|]*\|([^\]]*)\]\]", r"\1", text)
    text = re.sub(r"\[\[([^\]]*)\]\]", r"\1", text)
    text = htmlmod.unescape(text)
    text = re.sub(r"\s+", " ", text).strip()
    return text

def parse_table(tbl):
    """Parse a wikitable of packets into logical fields, honoring rowspan/colspan.

    Returns a list of field dicts: {name, type, notes, elements?}
    where 'elements' (when present) holds the sub-field rows grouped under a
    rowspan'd array/compound name+type cell.
    """
    raw_rows = []
    for part in re.split(r"^\s*\|-\s*$", tbl, flags=re.M)[1:]:
        cells = []
        for line in part.split("\n"):
            line = line.rstrip("\n")
            if not line.strip():
                continue
            if re.match(r"^\s*\|}\s*$", line):
                continue
            mcell = re.match(r"^\s*[|!]\s*", line)
            if not mcell:
                continue
            content = line[mcell.end():].strip()
            cell = {"text": content, "colspan": 1, "rowspan": 1, "cellspan": None}
            # parse leading colspan/rowspan attributes
            while True:
                ma = re.match(r'^\s*(?:"?colspan|"?rowspan)\s*=\s*"(\d+)"\s*\|',
                              cell["text"])
                if not ma:
                    break
                attr = cell["text"]
                if 'colspan' in attr.split('=')[0]:
                    cell["colspan"] = int(ma.group(1))
                else:
                    cell["rowspan"] = int(ma.group(1))
                cell["text"] = attr[ma.end():].strip()
            # explicit colspan: always a separator
            cell["text"] = cell["text"].strip()
            cells.append(cell)
        if cells:
            raw_rows.append(cells)

    # ---- expand into a positioned grid ----
    # grid[y][x] = cell dict, None = empty, or OCCUPIED where a spanning cell covers it
    OCCUPIED = object()
    grid = []
    y = 0
    for raw in raw_rows:
        if len(grid) <= y:
            grid.append([])
        x = 0
        for cell in raw:
            while grid[y][x] is not None if x < len(grid[y]) else False:
                x += 1
            # ensure enough rows/cols
            while len(grid) <= y + cell["rowspan"] - 1:
                grid.append([])
            for dy in range(cell["rowspan"]):
                row = grid[y + dy]
                while len(row) <= x + cell["colspan"] - 1:
                    row.append(None)
                for dx in range(cell["colspan"]):
                    if dy == 0 and dx == 0:
                        row[x + dx] = cell
                    else:
                        row[x + dx] = OCCUPIED
            x += cell["colspan"]
        y += 1

    # ---- find header row and column indices ----
    header_y = None
    name_width = 1
    type_width = 1
    for ri, row in enumerate(grid):
        texts = [c["text"] for c in row if isinstance(c, dict)]
        if any("Field Name" in t for t in texts) and any("Field Type" in t for t in texts):
            header_y = ri
            name_width = next((c["colspan"] for c in row if isinstance(c, dict) and "Field Name" in c["text"]), 1)
            type_width = next((c["colspan"] for c in row if isinstance(c, dict) and "Field Type" in c["text"]), 1)
            break
    if header_y is None:
        return []

    def col_of(keyword):
        for xi in range(len(grid[header_y])):
            c = grid[header_y][xi]
            if isinstance(c, dict) and keyword.lower() in c["text"].lower():
                return xi
        return None

    name_col = col_of("Field Name")
    type_col = col_of("Field Type")
    notes_col = col_of("Notes")
    if name_col is None or type_col is None:
        return []

    # element columns sit just past the name/type areas (colspan=2 layouts)
    elem_name_col = name_col + name_width
    elem_type_col = type_col + type_width

    def cell_at(yy, xx):
        if yy < len(grid) and xx < len(grid[yy]):
            c = grid[yy][xx]
            return c if isinstance(c, dict) else None
        return None

    # ---- extract logical fields, grouping rowspan'd array/compound cells ----
    fields = []
    y = header_y + 1
    while y < len(grid):
        name_cell = cell_at(y, name_col)
        if name_cell is None or name_cell["text"] in ("", "''", "''''"):
            y += 1
            continue
        # skip sub-header rows that reproduce column titles
        joined = name_cell["text"] + " " + " ".join(
            (c["text"] if isinstance(c, dict) and c["text"] else "" for c in grid[y] if c is not None))
        if "Field Name" in joined or "Action" == name_cell["text"].strip():
            y += 1
            continue

        field = {
            "name": name_cell["text"],
            "type": (cell_at(y, type_col)["text"] if cell_at(y, type_col) else ""),
            "notes": (clean_prose(cell_at(y, notes_col)["text"])
                      if notes_col is not None and cell_at(y, notes_col) else ""),
        }

        span = name_cell.get("rowspan", 1)
        # The array/compound "type" cell is only meaningful if it spans rows too;
        # otherwise it is the first element's own type (e.g. Boss Bar action tables).
        array_type_cell = cell_at(y, type_col)
        array_type_spans = bool(array_type_cell) and array_type_cell.get("rowspan", 1) > 1
        has_elements = span > 1
        if has_elements:
            elements = []
            for ey in range(y, y + span):
                # ordered dict cells in this row (skipping spanning OCCUPIED cells)
                cells = [c for c in grid[ey] if isinstance(c, dict)]
                # drop the spanning name cell and the spanning array-type cell
                drops = {id(name_cell)}
                if array_type_spans and array_type_cell is not None:
                    drops.add(id(array_type_cell))
                cells = [c for c in cells if id(c) not in drops]
                if cells and (cells[0]["text"] or (len(cells) > 1 and cells[1]["text"])):
                    elements.append({
                        "name": clean_prose(cells[0]["text"]) if cells[0]["text"] else None,
                        "type": strip_type_templates(cells[1]["text"]).strip()
                                if len(cells) > 1 and cells[1]["text"] else None,
                        "notes": clean_prose(cells[2]["text"]) if len(cells) > 2 and cells[2]["text"] else None,
                    })
            if elements:
                field["elements"] = elements

        fields.append(field)
        y += span if span > 1 else 1

    return fields

def field_type_and_max(cell):
    """Given a Field Type cell text, return (type, max_length or None, raw)."""
    cell = cell.strip()
    raw = cell
    maxlen = None
    mm = re.search(r"\((\d+)\)\s*$", cell)
    if mm:
        maxlen = int(mm.group(1))
        cell = cell[: mm.start()].strip()
    return cell, maxlen, raw

def clean_prose(text):
    """Strip wiki markup to plain text for notes."""
    text = re.sub(r"\{\{packet\|([^}|]+)(?:\|[^}]*)?\}\}", r"\1", text)
    text = re.sub(r"\{\{Type\|([^}|]+)(?:\|[^}]*)?\}\}", r"\1", text)
    text = re.sub(r"\{\{[^}]+\}\}", "", text)
    text = re.sub(r"\[\[[^\]|]*\|([^\]]*)\]\]", r"\1", text)
    text = re.sub(r"\[\[([^\]]*)\]\]", r"\1", text)
    text = re.sub(r"\[[^\] ]+ ([^\]]*)\]", r"\1", text)
    text = re.sub(r"''+", "", text)
    text = re.sub(r"<[^>]+>", "", text)
    text = htmlmod.unescape(text)
    text = re.sub(r"\s+", " ", text).strip()
    return text

# ---------------------------------------------------------------- index (html)
html = open(RAW_HTML, encoding="utf-8").read()
html = re.sub(r"<!--.*?-->", "", html, flags=re.S)

list_start = html.find('id="List_of_packets"')
list_end = html.find("<h2", list_start + 1)
# find the second <h2 ... > ("Packet format" is before; the next h2 after list is likely "Handshaking" definition)
# safer: find the first <h3 id="Handshaking" then the following <h2
second_hs = html.find('<h3 id="Handshaking"', list_start + 1)
next_h2 = html.find("<h2", second_hs)
if next_h2 != -1 and next_h2 < len(html):
    list_end = next_h2
section = html[list_start:list_end]

packet_index = []  # {state, direction, id, hex, name, official}
state = None
for m in re.finditer(
    r'<h3 id="(Handshaking|Status|Login|Configuration|Play)">.*?'
    r'<th colspan="4">(\w+) (\w+) packets\s*</th>',
    section, re.S,
):
    pass  # we handle below sequentially

# parse sequentially: <h3 id="State"> ... tables with caption "... clientbound packets"
# Each table block: <th colspan="4">STATE direction packets ... </tbody></table>
packet_index = []
for tm in re.finditer(
    r'<th colspan="4">(Handshaking|Status|Login|Configuration|Play) (clientbound|serverbound) packets\s*</th>(.*?)</tbody></table>',
    section, re.S,
):
    state = tm.group(1).lower()
    direction = tm.group(2)
    block = tm.group(3)
    for row in re.finditer(r'<tr class="">(.*?)</tr>', block, re.S):
        cells = re.findall(r'<td>(.*?)</td>', row.group(1), re.S)
        if len(cells) < 4:
            continue
        def clean(x):
            return htmlmod.unescape(re.sub(r"<[^>]+>", "", x)).strip()
        hex_text = clean(cells[1])
        if hex_text.lower().startswith("0x"):
            hex_text = hex_text[2:]
        packet_index.append({
            "state": state,
            "direction": direction,
            "id": int(clean(cells[0])),
            "hex": "0x" + hex_text.upper(),
            "name": clean(cells[2]),
            "official": clean(cells[3]),
        })

print("index packets:", len(packet_index))

# ------------------------------------------------------------- field defs (wiki)
wiki = open(RAW_WIKI, encoding="utf-8").read()

# find all packet definition blocks: ==== Name ==== ... first {| table ... |}
# We capture direction from the nearest preceding "=== Clientbound ===" / "=== Serverbound ==="
defs = {}  # (direction, name) -> fields and preamble
matches = list(re.finditer(r'^====\s*(.+?)\s*====\s*$', wiki, re.M))
for n, m in enumerate(matches):
    name = m.group(1).strip()
    start = m.end()
    end = matches[n + 1].start() if n + 1 < len(matches) else len(wiki)
    block = wiki[start:end]
    # determine direction from preceding === heading
    preceding = wiki[:m.start()]
    dirs = re.findall(r'^=== (Clientbound|Serverbound) ===\s*$', preceding, re.M)
    direction = dirs[-1].lower() if dirs else None
    # extract first wikitable
    table = re.search(r'(\{\|.*?\|\})', block, re.S)
    if not table:
        continue
    tbl = table.group(1)
    # preamble = text between heading and table
    preamble = block[: table.start()].strip()
    fields = parse_table(tbl)
    defs[(direction, name)] = {"fields": fields, "preamble": preamble}

print("def blocks:", len(defs))

# ------------------------------------------------------------- join
packets = []
missing = []
for p in packet_index:
    key = (p["direction"], p["name"])
    d = defs.get(key)
    fields = []
    if not d:
        missing.append((p["state"], p["direction"], p["id"], p["name"]))
        entry = {
            "state": p["state"],
            "direction": p["direction"],
            "packetId": p["id"],
            "packetIdHex": p["hex"],
            "name": p["name"],
            "officialName": p["official"],
            "unverified": True,
            "fields": [],
        }
        packets.append(entry)
        continue
    for f in d["fields"]:
        # skip header rows and 'no fields' filler
        joined = " ".join([f["name"] or "", f["type"] or ""])
        if "Field Name" in (f["name"] or "") or "no fields" in joined.lower():
            continue
        field = {
            "name": clean_prose(f["name"]) if f["name"] else None,
            "type": htmlmod.unescape(strip_type_templates(f["type"])).strip() if f["type"] else None,
            "notes": f["notes"] if f["notes"] else None,
        }
        if "elements" in f and f["elements"]:
            elems = []
            for e in f["elements"]:
                elems.append({
                    "name": clean_prose(e["name"]) if e["name"] else None,
                    "type": htmlmod.unescape(strip_type_templates(e["type"])).strip() if e["type"] else None,
                    "notes": e["notes"] if e["notes"] else None,
                })
            field["elements"] = elems
        fields.append(field)
    packets.append({
        "state": p["state"],
        "direction": p["direction"],
        "packetId": p["id"],
        "packetIdHex": p["hex"],
        "name": p["name"],
        "officialName": p["official"],
        "fields": fields,
    })

print("missing field defs:", len(missing))
for x in missing:
    print("  MISSING:", x)

# ------------------------------------------------------------- validation
# 1. unique (state, direction, packetId)
seen = {}
dups = []
for p in packets:
    k = (p["state"], p["direction"], p["packetId"])
    if k in seen:
        dups.append((seen[k], p["name"]))
    seen[k] = p["name"]
print("duplicate ids:", dups or "none")

# 2. contiguous ids 0..n-1 per (state, direction)
from collections import defaultdict
by_sd = defaultdict(list)
for p in packets:
    by_sd[(p["state"], p["direction"])].append(p["packetId"])
for k in sorted(by_sd):
    ids = sorted(by_sd[k])
    expected = list(range(len(ids)))
    if ids != expected:
        print("NON-CONTIGUOUS:", k, "expected", expected[:5], "..., got", ids[:5], "count", len(ids))

# ------------------------------------------------------------- write json
doc = {
    "_comment": "Generated from Minecraft Wiki 'Java Edition protocol/Packets' (CC BY-SA 3.0). "
                 "Target: Minecraft 26.3, protocol 777. "
                 "Raw source pages (gitignored): docs/data/_raw/_packets.raw.*",
    "version": "26.3",
    "protocol": 777,
    "packets": packets,
}

out = BASE + r"\packets.json"
with open(out, "w", encoding="utf-8") as f:
    json.dump(doc, f, indent=2, ensure_ascii=False)
    f.write("\n")

print("wrote", out, "packets:", len(packets))

# summary per state/direction
from collections import Counter
c = Counter((p["state"], p["direction"]) for p in packets)
for k in sorted(c):
    print(k, c[k])
