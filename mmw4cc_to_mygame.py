#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
MMW4CC (USC or JSON) -> MyGame converter (START-based long mapping)
- 入力: .usc も .json もOK（どちらも JSON）
- ディレクトリ指定時は再帰で .usc/.json を一括変換
- slide/guide は START の lane/size を採用、END の lane/size は無視
- hold = endBeat - startBeat (beats)
- 12レーン: center±size をレーン境界にスナップして lane(1..12)/width を算出
- 難易度名はファイル名に含まれるキーワードから推定（easy/normal/hard/expert/master）
- metadata.title があれば出力フォルダ名を title にリネーム（安全な文字に整形）
"""

import json, math, sys, os, re
from typing import Tuple, List, Dict
from pathlib import Path

DEFAULT_REVERSE = 1.0/6.0
ALLOWED_EXTS = {".usc", ".json"}

DIFF_KEYS = [
    ("master",  "master"),
    ("expert",  "expert"),
    ("extreme", "expert"),
    ("hard",    "hard"),
    ("normal",  "normal"),
    ("easy",    "easy"),
]

def clamp(v, a, b): return max(a, min(b, v))

def safe_folder_name(name: str) -> str:
    # フォルダ名に使いにくい文字を置換
    name = name.strip()
    name = re.sub(r"[\\/:*?\"<>|\n\r\t]", "_", name)
    return name[:100] if len(name) > 100 else name

def detect_difficulty_from_filename(path: str) -> str:
    fn = os.path.basename(path).lower()
    for key, val in DIFF_KEYS:
        if key in fn:
            return val
    return ""  # 不明なら空（chart.json にする）

def map_lane_width_from_center_size(center: float, size: float) -> Tuple[int,int]:
    # START-based: center±size を 12 レーンに写像
    left  = float(center) - float(size)
    right = float(center) + float(size)
    left_idx  = math.floor(left + 7.0)
    right_idx = math.ceil(right + 7.0)
    left_idx  = clamp(left_idx, 1, 12)
    right_idx = clamp(right_idx, left_idx + 1, 13)  # 最低幅1
    width = clamp(right_idx - left_idx, 1, 12)
    lane  = left_idx
    return lane, width

def load_usc_objects(path: str) -> Dict:
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
    # 標準の USC 形式を想定
    usc = data.get("usc", {})
    return usc

def first_bpm(objs) -> float:
    for obj in objs:
        if obj.get("type") == "bpm":
            try:
                bpm = float(obj.get("bpm", 0))
            except Exception:
                bpm = 0
            if bpm > 0:
                return bpm
    return 180.0

def build_speed_markers(objs, base_bpm: float):
    marks = {}
    # BPM 変化を timeScale に寄与させる（基準 BPM 比）
    for obj in objs:
        if obj.get("type") == "bpm":
            b  = float(obj.get("beat", 0))
            bv = float(obj.get("bpm", 0))
            if bv > 0 and base_bpm > 0:
                sc = base_bpm / bv
                m = marks.setdefault(b, {})
                m["forward"] = m.get("forward", 1.0) * sc
                m["reverse"] = m.get("reverse", DEFAULT_REVERSE) * sc
    # timeScaleGroup
    for obj in objs:
        if obj.get("type") == "timeScaleGroup":
            for ch in obj.get("changes", []):
                b  = float(ch.get("beat", 0))
                ts = float(ch.get("timeScale", 1.0))
                m = marks.setdefault(b, {})
                m["forward"] = m.get("forward", 1.0) * ts
                m["reverse"] = m.get("reverse", DEFAULT_REVERSE) * ts
    out = [{"beat": 0.0, "forward": 1.0, "reverse": DEFAULT_REVERSE}]
    for b in sorted(marks.keys()):
        if b == 0:
            out[0] = {"beat": 0.0,
                      "forward": float(marks[b].get("forward",1.0)),
                      "reverse": float(marks[b].get("reverse", DEFAULT_REVERSE))}
        else:
            out.append({"beat": float(b),
                        "forward": float(marks[b].get("forward",1.0)),
                        "reverse": float(marks[b].get("reverse", DEFAULT_REVERSE))})
    return out

def get_start_end_from_slide(obj):
    # START/END を connections から取得（START の lane/size を採用）
    conns = obj.get("connections", [])
    if isinstance(conns, list) and conns:
        starts = [c for c in conns if c.get("type") == "start"]
        ends   = [c for c in conns if c.get("type") == "end"]
        if starts and ends:
            s = starts[0]; e = ends[-1]
            s_beat = float(s.get("beat", obj.get("beat", 0)))
            e_beat = float(e.get("beat", s_beat))
            s_center = float(s.get("lane", obj.get("lane", 0)))
            s_size   = float(s.get("size", obj.get("size", 1)))
            return s_beat, e_beat, s_center, s_size
    # フォールバック
    s_beat = float(obj.get("startBeat", obj.get("beat", obj.get("time", 0))))
    e_beat = float(obj.get("endBeat",   obj.get("endTime", obj.get("end", s_beat))))
    s_center = float(obj.get("lane", 0))
    s_size   = float(obj.get("size", 1))
    return s_beat, e_beat, s_center, s_size

def convert_one_file(path_in: str, out_root: str):
    usc = load_usc_objects(path_in)
    objs = usc.get("objects", [])
    bpm  = first_bpm(objs)
    offset = float(usc.get("offset", 0.0)) if "offset" in usc else 0.0

    out_notes = []
    for obj in objs:
        if obj.get("trace") is True:
            continue
        t = obj.get("type")
        if t == "single":
            beat = float(obj.get("beat", 0))
            center = float(obj.get("lane", 0))
            size = float(obj.get("size", 1))
            lane, width = map_lane_width_from_center_size(center, size)
            has_dir = ("direction" in obj and obj["direction"] is not None)
            crit = bool(obj.get("critical", False))
            if (not crit) and (not has_dir):
                typ = "normal"
            elif crit and (not has_dir):
                typ = "critical"
            elif has_dir:
                typ = "flick"
            else:
                typ = "normal"
            out_notes.append({"time": beat, "lane": lane, "width": width, "type": typ})
        elif t in ("slide", "guide"):
            s_beat, e_beat, s_center, s_size = get_start_end_from_slide(obj)
            hold = max(0.0, e_beat - s_beat)
            lane, width = map_lane_width_from_center_size(s_center, s_size)  # START 基準
            out_notes.append({
                "time": s_beat, "lane": lane, "width": width,
                "type": ("long" if t == "slide" else "guide"),
                "hold": hold
            })

    out_notes.sort(key=lambda n: (n["time"], n["lane"]))
    markers = build_speed_markers(objs, bpm)

    # 出力先の曲フォルダ名：metadata.title があればそれに
    meta_title = None
    # usc の top-level に title が来るケースも一応見る
    if "title" in usc and isinstance(usc["title"], str) and usc["title"].strip():
        meta_title = usc["title"].strip()
    # objects内や別構造にタイトルが無い場合は None

    # フォルダ名
    folder_name = meta_title if meta_title else os.path.splitext(os.path.basename(path_in))[0]
    folder_name = safe_folder_name(folder_name)
    song_dir = os.path.join(out_root, folder_name)
    os.makedirs(song_dir, exist_ok=True)

    # 難易度ファイル名
    diff = detect_difficulty_from_filename(path_in)
    chart_name = f"{diff}.json" if diff else "chart.json"
    chart_path = os.path.join(song_dir, chart_name)

    # 書き出し
    with open(chart_path, "w", encoding="utf-8") as f:
        json.dump(out_notes, f, ensure_ascii=False, indent=2)

    meta_out = {
        "title": meta_title if meta_title else folder_name,
        "bpm": bpm,
        "offset": offset,
        "speedScaleMarkers": markers
    }
    with open(os.path.join(song_dir, "metadata.json"), "w", encoding="utf-8") as f:
        json.dump(meta_out, f, ensure_ascii=False, indent=2)

    print(f"[OK] {path_in} -> {chart_path}")

def convert_one_file_flat(path_in: str, out_dir: str, out_name: str):
    """
    Convert one USC/JSON file and write chart JSON directly under out_dir with the given out_name.
    Returns a tuple (meta_title, bpm, offset, markers).
    Does NOT write metadata.json here (caller writes once for folder).
    """
    usc = load_usc_objects(path_in)
    objs = usc.get("objects", [])
    bpm  = first_bpm(objs)
    offset = float(usc.get("offset", 0.0)) if "offset" in usc else 0.0

    out_notes = []
    for obj in objs:
        if obj.get("trace") is True:
            continue
        t = obj.get("type")
        if t == "single":
            beat = float(obj.get("beat", 0))
            center = float(obj.get("lane", 0))
            size = float(obj.get("size", 1))
            lane, width = map_lane_width_from_center_size(center, size)
            has_dir = ("direction" in obj and obj["direction"] is not None)
            crit = bool(obj.get("critical", False))
            if (not crit) and (not has_dir):
                typ = "normal"
            elif crit and (not has_dir):
                typ = "critical"
            elif has_dir:
                typ = "flick"
            else:
                typ = "normal"
            out_notes.append({"time": beat, "lane": lane, "width": width, "type": typ})
        elif t in ("slide", "guide"):
            s_beat, e_beat, s_center, s_size = get_start_end_from_slide(obj)
            hold = max(0.0, e_beat - s_beat)
            lane, width = map_lane_width_from_center_size(s_center, s_size)  # START 基準
            out_notes.append({
                "time": s_beat, "lane": lane, "width": width,
                "type": ("long" if t == "slide" else "guide"),
                "hold": hold
            })

    out_notes.sort(key=lambda n: (n["time"], n["lane"]))
    markers = build_speed_markers(objs, bpm)

    os.makedirs(out_dir, exist_ok=True)
    chart_path = os.path.join(out_dir, out_name)
    with open(chart_path, "w", encoding="utf-8") as f:
        json.dump(out_notes, f, ensure_ascii=False, indent=2)

    meta_title = None
    if "title" in usc and isinstance(usc["title"], str) and usc["title"].strip():
        meta_title = usc["title"].strip()

    print(f"[OK] {path_in} -> {chart_path}")
    return meta_title, bpm, offset, markers

def collect_inputs(target: str) -> List[str]:
    paths = []
    if os.path.isfile(target):
        ext = os.path.splitext(target)[1].lower()
        if ext in ALLOWED_EXTS:
            paths.append(os.path.abspath(target))
    else:
        for root, _, files in os.walk(target):
            for fn in files:
                ext = os.path.splitext(fn)[1].lower()
                if ext in ALLOWED_EXTS:
                    paths.append(os.path.join(root, fn))
    return paths

def infer_out_name(path: Path, default_name: str) -> str:
    diff = detect_difficulty_from_filename(str(path))
    return f"{diff}.json" if diff else default_name

def main():
    if len(sys.argv) < 3:
        print("Usage:")
        print("  python mmw4cc_to_mygame.py <input(.usc/.json or dir)> <out_root_dir>")
        sys.exit(1)

    target  = sys.argv[1]
    outroot = sys.argv[2]
    os.makedirs(outroot, exist_ok=True)

    p1 = Path(target)
    if p1.is_dir():
        total = ok = ng = 0
        # 出力は outroot の直下に a.json, b.json ... とし、metadata.json は一個だけ作成
        folder_title = os.path.basename(os.path.normpath(p1))
        first_meta = None  # (meta_title, bpm, offset, markers)

        # .usc および .json を収集
        inputs = collect_inputs(p1)
        for src in inputs:
            total += 1
            try:
                out_name = infer_out_name(Path(src), Path(src).stem + ".json")
                meta_title, bpm, offset, markers = convert_one_file_flat(src, outroot, out_name)
                if first_meta is None:
                    first_meta = (meta_title, bpm, offset, markers)
                ok += 1
            except Exception as e:
                print(f"[NG] {src}: {e}")
                ng += 1

        # metadata.json を一個だけ outroot に書く（title は入力フォルダ名）
        if first_meta is not None:
            _, bpm0, offset0, markers0 = first_meta
            meta_out = {
                "title": folder_title,
                "bpm": bpm0,
                "offset": offset0,
                "speedScaleMarkers": markers0
            }
            with open(os.path.join(outroot, "metadata.json"), "w", encoding="utf-8") as f:
                json.dump(meta_out, f, ensure_ascii=False, indent=2)
            print(f"[META] {os.path.join(outroot, 'metadata.json')} written (title={folder_title})")
        else:
            print("[WARN] 変換対象が無かったため metadata.json は作成しませんでした。")

        print(f"== 完了 total={total}, ok={ok}, ng={ng} ==")
        return

    inputs = collect_inputs(target)
    if not inputs:
        print("[WARN] 対象ファイル(.usc/.json)が見つかりません。")
        sys.exit(0)

    for p in inputs:
        try:
            convert_one_file(p, outroot)
        except Exception as e:
            print(f"[ERR] 変換失敗: {p} -> {e}")

if __name__ == "__main__":
    main()