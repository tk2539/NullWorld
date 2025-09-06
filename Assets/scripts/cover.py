#!/usr/bin/env python3
# rename_pngs.py
import os
import argparse
import sys

def main():
    ap = argparse.ArgumentParser(description="Rename all .png files in subfolders to original.png")
    ap.add_argument("--base", required=True, help="Base folder path (e.g., /Users/tk/rhythm-game/Assets/StreamingAssets/charts)")
    args = ap.parse_args()

    base = args.base
    if not os.path.isdir(base):
        print(f"Base not found or not a directory: {base}", file=sys.stderr)
        sys.exit(1)

    changed = skipped = 0

    for entry in sorted(os.listdir(base)):
        subdir = os.path.join(base, entry)
        if not os.path.isdir(subdir):
            continue

        pngs = [f for f in os.listdir(subdir) if f.lower().endswith(".png")]
        if not pngs:
            skipped += 1
            continue

        for f in pngs:
            old_path = os.path.join(subdir, f)
            new_path = os.path.join(subdir, "original.png")
            os.rename(old_path, new_path)
            changed += 1

    print(f"Done. renamed={changed}, skipped(no .png)={skipped}")

if __name__ == "__main__":
    main()