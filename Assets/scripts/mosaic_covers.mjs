#!/usr/bin/env node
// mosaic_covers.mjs
// Python版 mosaic_covers.py の Node移植 (sharp使用)
// - charts/各曲フォルダの original.png/jpg/jpeg を探し、cover.png を作成
// - モザイクは「cells x cells」に縮小(=面積平均相当)→最近傍で目的サイズへ拡大

import fs from 'node:fs';
import fsp from 'node:fs/promises';
import path from 'node:path';
import readline from 'node:readline/promises';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

function askFactory() {
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  return {
    async ask(prompt, def) {
      const suffix = def ? ` [${def}]` : '';
      const ans = (await rl.question(`${prompt}${suffix}: `)).trim();
      return ans || (def ?? '');
    },
    async yesno(prompt, defaultNo = true) {
      const suffix = defaultNo ? 'y/N' : 'Y/n';
      const ans = (await rl.question(`${prompt} [${suffix}]: `)).trim().toLowerCase();
      if (!ans) return !defaultNo;
      return ans.startsWith('y');
    },
    close() { rl.close(); }
  };
}

async function exists(p) {
  try { await fsp.access(p, fs.constants.F_OK); return true; }
  catch { return false; }
}

function toRGBA(sh) {
  // sharpはデフォでα持たない場合があるので ensureAlpha で透過対応
  return sh.ensureAlpha();
}

async function findOriginal(dirPath, base = 'original', allowAny = true) {
  const list = await fsp.readdir(dirPath, { withFileTypes: true });
  const targetPrefix = (base.toLowerCase() + '.');
  // まず original.* を探す
  for (const ent of list) {
    if (!ent.isFile()) continue;
    const name = ent.name.toLowerCase();
    if (
      name.startsWith(targetPrefix) &&
      (name.endsWith('.png') || name.endsWith('.jpg') || name.endsWith('.jpeg'))
    ) {
      return path.join(dirPath, ent.name);
    }
  }
  if (allowAny) {
    // フォールバック：ディレクトリ内の最初の画像を使う
    for (const ent of list) {
      if (!ent.isFile()) continue;
      const name = ent.name.toLowerCase();
      if (name.endsWith('.png') || name.endsWith('.jpg') || name.endsWith('.jpeg')) {
        return path.join(dirPath, ent.name);
      }
    }
  }
  return null;
}

// dirPath: 1曲フォルダ
async function processDir(dirPath, cells, outSize, overwrite, outputName = 'cover.png') {
  try {
    const src = await findOriginal(dirPath);
    if (!src) return `SKIP(no original.*): ${dirPath}`;

    const outPath = path.join(dirPath, outputName);
    if (!overwrite && await exists(outPath)) {
      return `SKIP(exists): ${outPath}`;
    }

    // 1) 読み込み → RGBA化
    const input = sharp(src, { failOn: 'none' });
    const meta = await input.metadata();
    if (!meta || (!meta.width && !meta.height)) {
      return `SKIP(invalid image meta): ${src}`;
    }
    if (!meta || !meta.width || !meta.height) {
      return `ERR(${dirPath}): invalid image`;
    }

    // 2) cells x cells に縮小（sharpは縮小時に領域平均をとる挙動なので BOX 相当）
    const smallBuf = await toRGBA(input)
      .resize(cells, cells, { fit: 'fill', kernel: sharp.kernel.lanczos3 }) // 縮小時はLanczosでもOK（平均寄り）
      .png()
      .toBuffer();

    // 3) 最近傍で outSize x outSize に拡大（ピクセル感出す）
    const mosaic = sharp(smallBuf).resize(outSize, outSize, { fit: 'fill', kernel: sharp.kernel.nearest });

    await mosaic.png().toFile(outPath);
    return `OK: ${outPath}`;
  } catch (e) {
    return `ERR(${dirPath}): ${e.message || e}`;
  }
}

async function* walkDirs(root, includeSubdirs) {
  // まずルート自体を処理
  yield root;
  if (!includeSubdirs) {
    const list = await fsp.readdir(root, { withFileTypes: true });
    for (const ent of list) {
      if (ent.isDirectory()) yield path.join(root, ent.name);
    }
  } else {
    // 再帰（ルートはすでにyieldしたので子から）
    async function* rec(p) {
      const list = await fsp.readdir(p, { withFileTypes: true });
      for (const ent of list) {
        const full = path.join(p, ent.name);
        if (ent.isDirectory()) {
          yield full;
          yield* rec(full);
        }
      }
    }
    yield* rec(root);
  }
}

async function main() {
  const ask = askFactory();

  const defaultRoot = path.resolve(process.cwd(), 'Assets', 'StreamingAssets', 'charts');
  const chartsRoot = path.resolve(await ask.ask('charts ルートのパス', defaultRoot));
  if (!await exists(chartsRoot)) {
    console.error(`指定パスが見つかりません: ${chartsRoot}`);
    ask.close();
    process.exit(1);
  }

  const cellsStr = await ask.ask('グリッド数（nで n×n）', '8');
  const outStr   = await ask.ask('出力サイズ（px）', '1024');
  const overwrite = await ask.yesno('既存 cover を上書きしますか？', true);
  const includeSubdirs = await ask.yesno('サブフォルダも含めますか？', true);
  ask.close();

  const cells = Number.parseInt(cellsStr, 10);
  const outSize = Number.parseInt(outStr, 10);
  if (!Number.isFinite(cells) || cells <= 0 || !Number.isFinite(outSize) || outSize <= 0) {
    console.error('数値入力が不正です。');
    process.exit(1);
  }

  let ok = 0, skip = 0, err = 0;
  for await (const d of walkDirs(chartsRoot, includeSubdirs)) {
    const msg = await processDir(d, cells, outSize, overwrite);
    if (msg.startsWith('OK')) ok++;
    else if (msg.startsWith('ERR')) err++;
    else skip++;
    console.log(msg);
  }

  console.log(`\n=== DONE ===\nGenerated: ${ok}\nSkipped: ${skip}\nErrors: ${err}`);
}

if (import.meta.url === `file://${__filename}`) {
  main().catch(e => {
    console.error(e);
    process.exit(1);
  });
}