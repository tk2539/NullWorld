#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
全ての metadata.json から offset, rootoffsetMs を削除し、
"rootoffset": 1200 を追加するスクリプト。

使い方:
    python fix_metadata.py --root Assets/StreamingAssets/charts
"""

import json
import argparse
from pathlib import Path

def process_metadata(meta_path: Path):
    try:
        data = json.loads(meta_path.read_text(encoding="utf-8"))
    except Exception as e:
        print(f"[ERR] {meta_path}: JSON読み込み失敗: {e}")
        return

    # 不要キー削除
    data.pop("offset", None)
    data.pop("rootoffsetMs", None)

    # rootoffsetを1200に設定
    data["rootoffsetMs"] = 1200

    try:
        meta_path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"[OK] {meta_path}")
    except Exception as e:
        print(f"[ERR] {meta_path}: 書き込み失敗: {e}")


def main():
    parser = argparse.ArgumentParser(description="metadata.jsonを一括修正")
    parser.add_argument("--root", required=True, help="chartsフォルダのパス")
    args = parser.parse_args()

    root = Path(args.root)
    if not root.exists():
        print(f"指定フォルダが存在しません: {root}")
        return

    count = 0
    for meta in root.rglob("metadata.json"):
        process_metadata(meta)
        count += 1

    print(f"=== 完了: {count}ファイル修正 ===")


if __name__ == "__main__":
    main()