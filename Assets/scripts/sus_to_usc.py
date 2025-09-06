#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
Recursive SUS -> USC batch converter.
- Walks through given folder (including subfolders)
- For each .sus, writes a same-name .usc in the same directory
- USC schema: {"version":2, "usc":{"objects":[...], "offset":-0.0}}

Usage:
  python sus_batch_to_usc.py /path/to/root
"""

import os
import re
import json
import sys
import collections

# ===== Core converter =====

def sus_to_usc(in_path, out_path):
    """
    Convert a single .sus file into .usc (version=2).
    Mapping policy (generic, tweak if you have strict chart spec):
      - Channel 1x -> "single"
      - Channel 5x -> slide ticks; contiguous (<= 1/4 beat) are grouped into one slide:
            start + ticks + end on the same lane
      - Lane mapping: HEX 1..C -> centers from about -5.5..+5.5 (step 1)
      - Size: 1.5 (generic)
      - Put default BPM at beat 0.0 if #BPM01 or #BPMDEFAULT not present -> 120.0
      - Emits {"version":2, "usc":{"objects":[...], "offset":-0.0}}
    """
    # Read lines safely
    with open(in_path, encoding="utf-8", errors="ignore") as f:
        lines = [ln.rstrip() for ln in f]

    # Defaults & parsed meta
    ticks_per_beat = 480
    bpm_default = None
    bpm_map = {}  # e.g., BPM01 -> value
    measure_beats = collections.defaultdict(lambda: 4.0)  # beats per measure (4/4 -> 4)

    # Parse global directives
    for ln in lines:
        if ln.startswith("#REQUEST") and "ticks_per_beat" in ln:
            m = re.search(r"ticks_per_beat\s+(\d+)", ln)
            if m:
                ticks_per_beat = int(m.group(1))
        elif ln.startswith("#BPM") and ":" in ln and re.match(r"#BPM[0-9A-Fa-f]{2}:", ln):
            # e.g. "#BPM01: 150"
            tag, val = ln.split(":", 1)
            key = tag[1:].strip()  # BPM01
            try:
                bpm_map[key] = float(val.strip())
            except:
                pass
        elif ln.startswith("#BPMDEFAULT"):
            # e.g. "#BPMDEFAULT 150"
            try:
                bpm_default = float(ln.split()[1])
            except:
                pass
        elif ln.startswith("#MEASURE"):
            # e.g. "#MEASURE012: 4/4"
            m = re.match(r"#MEASURE(\d{3}):\s*(\d+)\s*/\s*(\d+)", ln)
            if m:
                meas = int(m.group(1))
                a = int(m.group(2))
                b = int(m.group(3))
                # Convert X/Y into how many beats this measure has if quarter-note is 1 beat
                measure_beats[meas] = 4.0 * a / b

    # Resolve default BPM
    if bpm_default is None and "BPM01" in bpm_map:
        bpm_default = bpm_map["BPM01"]
    if bpm_default is None:
        bpm_default = 120.0  # fallback

    # Parse measure/channel lines
    def parse_measure_channel(line):
        # Patterns like "#01216:00120000" or "#0125B:..."
        m = re.match(r"#(\d{3})([0-9A-Fa-f]{2,3}):\s*([0-9A-Fa-f]+)", line)
        if not m:
            return None
        meas = int(m.group(1))
        ch = m.group(2).upper()
        data = m.group(3).replace(" ", "")
        if len(data) % 2 != 0:
            data = "0" + data  # pad
        tokens = [data[i:i+2] for i in range(0, len(data), 2)]
        return meas, ch, tokens

    # Lane center mapping from last hex digit 1..C to roughly [-5.5 .. +5.5]
    def lane_center_from_hex(hex_digit: int) -> float:
        # Empirical mapping compatible with USC samples: center = (h - 7) + 0.5
        # h=1 -> -5.5, h=7 -> 0.5, h=C(12) -> 5.5
        return float((hex_digit - 7) + 0.5)

    # Collect events
    objects = []
    # Default BPM and a default timeScaleGroup (index 0) at top
    objects.append({"type": "bpm", "beat": 0.0, "bpm": float(bpm_default)})
    objects.append({"type": "timeScaleGroup", "changes": []})

    # Raw points
    single_points = []
    slide_points = []

    for ln in lines:
        parsed = parse_measure_channel(ln)
        if not parsed:
            continue
        meas, ch, tokens = parsed
        grp = ch[0]  # '1', '5', etc.

        # We only translate 1x (tap/single) and 5x (slide path) in this generic converter
        if grp not in ("1", "5"):
            continue

        try:
            lane_hex = int(ch[-1], 16)
        except:
            continue  # skip if hex-digit not parseable

        beats_in_measure = measure_beats[meas]
        seg = len(tokens)
        for idx, tok in enumerate(tokens):
            if tok.upper() == "00":
                continue
            # Convert index inside measure to absolute beat
            beat = meas * 4.0 + (idx / seg) * beats_in_measure
            lane_center = lane_center_from_hex(lane_hex)
            entry = {"beat": round(beat, 6), "lane": lane_center, "size": 1.5}
            if grp == "1":
                single_points.append(entry)
            elif grp == "5":
                slide_points.append(entry)

    # Deduplicate exact same (beat, lane, size)
    def dedup(points):
        seen = set()
        out = []
        for e in points:
            key = (e["beat"], e["lane"], e["size"])
            if key in seen:
                continue
            seen.add(key)
            out.append(e)
        return out

    single_points = dedup(single_points)
    slide_points = dedup(slide_points)

    # Emit singles
    for e in single_points:
        objects.append({
            "type": "single",
            "beat": e["beat"],
            "lane": e["lane"],
            "size": e["size"],
            "critical": False,
            "timeScaleGroup": 0,
            "trace": False
        })

    # Group slide ticks on same lane into runs, then emit start/tick/end
    # Heuristic: adjacent points within <= 1/4 beat belong to the same slide-run.
    eps = 1e-6
    slide_by_lane = collections.defaultdict(list)
    for e in slide_points:
        slide_by_lane[e["lane"]].append(e["beat"])

    for lane, beats in slide_by_lane.items():
        beats = sorted(set(beats))
        if not beats:
            continue
        runs = []
        cur = [beats[0]]
        for a, b in zip(beats, beats[1:]):
            if (b - a) <= 0.25 + eps:
                cur.append(b)
            else:
                runs.append(cur)
                cur = [b]
        if cur:
            runs.append(cur)

        for run in runs:
            first, last = run[0], run[-1]
            objects.append({"type": "start", "beat": first, "lane": lane, "size": 1.5,
                            "critical": False, "timeScaleGroup": 0})
            for bt in run:
                objects.append({"type": "tick", "beat": bt, "lane": lane, "size": 1.5,
                                "critical": False, "timeScaleGroup": 0})
            objects.append({"type": "end", "beat": last, "lane": lane, "size": 1.5,
                            "critical": False, "timeScaleGroup": 0})

    # Sort objects by (beat, type-priority)
    type_priority = {"bpm": 0, "timeScaleGroup": 1}
    objects_sorted = sorted(objects, key=lambda o: (o.get("beat", 0.0), type_priority.get(o["type"], 2)))

    usc_out = {"version": 2, "usc": {"objects": objects_sorted, "offset": -0.0}}

    # Write
    os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(usc_out, f, ensure_ascii=False, indent=2)

    return collections.Counter(o["type"] for o in objects_sorted)


# ===== Batch (recursive) runner =====

def batch_convert_recursive(root_dir: str):
    """
    Walk root_dir recursively, convert every .sus into same-name .usc
    in the same directory. Returns (ok_count, err_count).
    """
    ok = 0
    err = 0
    for cur, _dirs, files in os.walk(root_dir):
        for fname in files:
            if not fname.lower().endswith(".sus"):
                continue
            in_path = os.path.join(cur, fname)
            out_path = os.path.join(cur, os.path.splitext(fname)[0] + ".usc")
            try:
                stats = sus_to_usc(in_path, out_path)
                print(f"[OK] {in_path} -> {out_path}  {dict(stats)}")
                ok += 1
            except Exception as e:
                print(f"[ERROR] {in_path}: {e}")
                err += 1
    return ok, err


# ===== CLI =====

def main():
    if len(sys.argv) < 2:
        print("Usage: python sus_batch_to_usc.py <root_folder>")
        sys.exit(1)

    root = sys.argv[1]
    if not os.path.isdir(root):
        print(f"Not a directory: {root}")
        sys.exit(1)

    ok, err = batch_convert_recursive(root)
    print(f"\nDone. Converted: {ok}, Errors: {err}")

if __name__ == "__main__":
    main()