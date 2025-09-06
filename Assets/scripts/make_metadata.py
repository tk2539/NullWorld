#!/usr/bin/env python3
# make_metadata.py
import os, json, argparse, sys

TEMPLATE = {
    "title": None,             # ← フォルダー名で埋める
    "artist": None,
    "bpm": None,
    "difficulties": {
        "easy": None,
        "normal": None,
        "hard": None,
        "expert": None,
        "master": None
    },
    "speedScaleMarkers": []
}

def main():
    ap = argparse.ArgumentParser(description="Create metadata.json in each subfolder.")
    ap.add_argument("--base", required=True, help="Base folder path (e.g., /Users/tk/Downloads/pjsk)")
    ap.add_argument("--overwrite", action="store_true", help="Overwrite existing metadata.json")
    args = ap.parse_args()

    base = args.base
    if not os.path.isdir(base):
        print(f"Base not found or not a directory: {base}", file=sys.stderr)
        sys.exit(1)

    made = updated = skipped = 0

    # サブフォルダーだけを対象にする
    for entry in sorted(os.listdir(base)):
        subdir = os.path.join(base, entry)
        if not os.path.isdir(subdir):
            continue

        target = os.path.join(subdir, "metadata.json")
        if os.path.exists(target) and not args.overwrite:
            skipped += 1
            continue

        data = TEMPLATE.copy()
        data["title"] = entry  # フォルダー名をそのまま使う

        # JSONをUTF-8（日本語などもそのまま）で書き出す
        with open(target, "w", encoding="utf-8", newline="\n") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
            f.write("\n")

        if os.path.exists(target) and args.overwrite:
            updated += 1
        else:
            made += 1

    print(f"Done. created={made}, updated={updated}, skipped={skipped}")

if __name__ == "__main__":
    main()