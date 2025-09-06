import json
import sys
import os

LANE_TARGETS = [1, 4, 7, 10]  # 近い数に丸める候補
FIXED_WIDTH = 3

def nearest_lane(lane: int) -> int:
    return min(LANE_TARGETS, key=lambda x: abs(x - lane))

def process_chart(in_path: str, out_path: str):
    with open(in_path, "r", encoding="utf-8") as f:
        notes = json.load(f)

    for note in notes:
        if "lane" in note:
            note["lane"] = nearest_lane(int(note["lane"]))
        note["width"] = FIXED_WIDTH

    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(notes, f, ensure_ascii=False, indent=2)

    print(f"[OK] {in_path} → {out_path}, {len(notes)} notes")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("使い方: python3 normalize_lanes.py input.json [output.json]")
        sys.exit(1)

    in_file = sys.argv[1]
    out_file = sys.argv[2] if len(sys.argv) > 2 else (
        os.path.splitext(in_file)[0] + "_norm.json"
    )

    process_chart(in_file, out_file)