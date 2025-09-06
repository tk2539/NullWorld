#!/usr/bin/env python3
import os, sys
from pathlib import Path
from typing import Optional
try:
    from PIL import Image
except ImportError:
    print("Pillow が見つかりません。先に `python3 -m pip install pillow` を実行してください。")
    sys.exit(1)

def ask(prompt: str, default: Optional[str] = None) -> str:
    s = input(f"{prompt}" + (f" [{default}]" if default else "") + ": ").strip()
    return s if s else (default or "")

def yesno(prompt: str, default_no=True) -> bool:
    s = input(f"{prompt} [{'y/N' if default_no else 'Y/n'}]: ").strip().lower()
    if not s: return not default_no
    return s.startswith('y')

def find_original(dir_path: Path, base="original") -> Optional[Path]:
    base_lower = base.lower() + "."
    for p in dir_path.iterdir():
        if not p.is_file(): 
            continue
        name = p.name.lower()
        if name.startswith(base_lower) and (name.endswith(".png") or name.endswith(".jpg") or name.endswith(".jpeg")):
            return p
    return None

def make_mosaic(img: Image.Image, cells: int, out_size: int) -> Image.Image:
    # RGBA に統一
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    # 1) cells x cells に縮小（BOX=面積平均でブロック平均を取るのと等価）
    small = img.resize((cells, cells), resample=Image.BOX)
    # 2) 仕上げに最近傍で拡大（ピクセル感を残す）
    mosaic = small.resize((out_size, out_size), resample=Image.NEAREST)
    return mosaic

def process_dir(dir_path: Path, cells: int, out_size: int, overwrite: bool, output_name="cover.png") -> str:
    src = find_original(dir_path)
    if not src:
        return f"SKIP(no original.*): {dir_path}"
    out_path = dir_path / output_name
    if out_path.exists() and not overwrite:
        return f"SKIP(exists): {out_path}"
    try:
        with Image.open(src) as im:
            mosaic = make_mosaic(im, cells, out_size)
            mosaic.save(out_path)  # PNG
        return f"OK: {out_path}"
    except Exception as e:
        return f"ERR({dir_path}): {e}"

def walk_dirs(root: Path, include_subdirs: bool):
    if not include_subdirs:
        for p in root.iterdir():
            if p.is_dir():
                yield p
    else:
        for p in root.rglob("*"):
            if p.is_dir():
                yield p

def main():
    default_root = Path.cwd() / "Assets" / "StreamingAssets" / "charts"
    charts_root = Path(ask("charts ルートのパス", str(default_root))).expanduser().resolve()
    if not charts_root.exists():
        print(f"指定パスが見つかりません: {charts_root}")
        sys.exit(1)

    try:
        cells = int(ask("グリッド数（nで n×n）", "8"))
        out_size = int(ask("出力サイズ（px）", "1024"))
    except ValueError:
        print("数値の入力が不正です。")
        sys.exit(1)

    overwrite = yesno("既存 cover を上書きしますか？", default_no=True)
    include_subdirs = yesno("サブフォルダも含めますか？", default_no=True)

    ok = skip = err = 0
    for d in walk_dirs(charts_root, include_subdirs):
        msg = process_dir(d, cells, out_size, overwrite)
        if msg.startswith("OK"):
            ok += 1
        elif msg.startswith("ERR"):
            err += 1
        else:
            skip += 1
        print(msg)

    print(f"\n=== DONE ===\nGenerated: {ok}\nSkipped: {skip}\nErrors: {err}")

if __name__ == "__main__":
    main()