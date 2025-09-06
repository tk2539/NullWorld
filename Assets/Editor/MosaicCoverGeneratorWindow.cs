#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// StreamingAssets/charts/*/original.(png|jpg|jpeg) を8x8等分の平均色モザイクにして
/// cover.png を生成するエディタ拡張。非破壊（originalは触らない）
/// </summary>
public class MosaicCoverGeneratorWindow : EditorWindow
{
    // 基本設定
    private string chartsRoot;                    // デフォルトは {StreamingAssets}/charts
    private string sourceBaseName = "original";   // 探すファイル名のベース
    private string outputFileName = "cover.png";  // 生成ファイル名（PNG固定）
    private int gridCells = 8;                    // 8x8
    private int outputSize = 1024;                // 仕上がり解像度（正方）
    private bool overwrite = true;                // 既存cover.pngを上書きするか
    private bool includeSubdirs = false;          // charts配下の入れ子を辿るか
    private bool verboseLog = true;               // 詳細ログを出すかどうか

    private readonly string[] exts = new[] { ".png", ".jpg", ".jpeg" };

    [MenuItem("Tools/Covers/Generate Mosaic Covers")]
    public static void Open()
    {
        var win = GetWindow<MosaicCoverGeneratorWindow>("Mosaic Covers");
        win.minSize = new Vector2(420, 260);
        win.Show();
    }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(chartsRoot))
            chartsRoot = Path.Combine(Application.streamingAssetsPath, "charts");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Mosaic Cover Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Paths", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Charts Root (read original.* / write cover.png)");
            EditorGUILayout.TextField(chartsRoot);
            if (GUILayout.Button("Pick Charts Folder..."))
            {
                string picked = EditorUtility.OpenFolderPanel("Select charts root", chartsRoot, "");
                if (!string.IsNullOrEmpty(picked)) chartsRoot = picked;
            }
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            sourceBaseName = EditorGUILayout.TextField(new GUIContent("Source Basename", "e.g. original -> original.png/jpg"), sourceBaseName);
            outputFileName = EditorGUILayout.TextField(new GUIContent("Output File Name", "PNG推奨。例: cover.png"), outputFileName);

            gridCells = EditorGUILayout.IntSlider(new GUIContent("Grid Cells", "8で8x8"), gridCells, 2, 64);
            outputSize = EditorGUILayout.IntPopup(
                "Output Size",
                outputSize,
                new string[] { "512", "768", "1024", "2048" },
                new int[]    { 512,  768,   1024,   2048 }
            );

            overwrite = EditorGUILayout.Toggle(new GUIContent("Overwrite cover"), overwrite);
            includeSubdirs = EditorGUILayout.Toggle(new GUIContent("Include Subdirectories"), includeSubdirs);
            verboseLog = EditorGUILayout.Toggle(new GUIContent("Verbose Log"), verboseLog);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate", GUILayout.Height(36)))
        {
            GenerateAll();
        }
    }

    private void GenerateAll()
    {
        if (string.IsNullOrEmpty(chartsRoot) || !Directory.Exists(chartsRoot))
        {
            EditorUtility.DisplayDialog("Error", "Charts root not found:\n" + chartsRoot, "OK");
            return;
        }

        var dirs = new List<string>(Directory.GetDirectories(chartsRoot));
        if (includeSubdirs)
        {
            // 深い入れ子も対象にする場合
            var stack = new Stack<string>(dirs);
            dirs.Clear();
            while (stack.Count > 0)
            {
                var d = stack.Pop();
                dirs.Add(d);
                foreach (var sd in Directory.GetDirectories(d))
                    stack.Push(sd);
            }
        }

        int ok = 0, skip = 0, fail = 0;
        if (verboseLog)
        {
            Debug.Log($"[MosaicCover] chartsRoot: {chartsRoot}, directories count: {dirs.Count}");
        }
        try
        {
            for (int i = 0; i < dirs.Count; i++)
            {
                string dir = dirs[i];
                EditorUtility.DisplayProgressBar("Mosaic Covers", Path.GetFileName(dir), (float)i / Mathf.Max(1, dirs.Count - 1));
                if (TryGenerateForDir(dir)) ok++;
                else skip++;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[MosaicCover] Exception: " + ex);
            fail++;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog("Done", $"Generated: {ok}\nSkipped: {skip}\nErrors: {fail}", "OK");
    }

    private bool TryGenerateForDir(string dir)
    {
        // original.* を大小文字無視で探す（優先: cover指定ではなく originalベース）
        string src = null;
        try
        {
            foreach (var f in Directory.GetFiles(dir))
            {
                string name = Path.GetFileName(f);
                string lower = name.ToLowerInvariant();
                if (!lower.StartsWith(sourceBaseName.ToLowerInvariant() + ".")) continue;
                foreach (var e in exts)
                {
                    if (lower.EndsWith(e)) { src = f; break; }
                }
                if (src != null) break;
            }
        }
        catch { /* ignore */ }

        if (string.IsNullOrEmpty(src))
        {
            // 見つからなければスキップ
            if (verboseLog) Debug.Log($"[MosaicCover] skip(no source): {dir}");
            // Debug.Log($"[MosaicCover] no source in {dir}");
            return false;
        }

        string outPath = Path.Combine(dir, outputFileName);
        if (!overwrite && File.Exists(outPath))
        {
            // 既にcoverがあればスキップ
            if (verboseLog) Debug.Log($"[MosaicCover] skip(existing cover): {outPath}");
            return false;
        }

        // 読み込み
        var bytes = File.ReadAllBytes(src);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false); // readable
        if (!ImageConversion.LoadImage(tex, bytes, false)) // markNonReadable = false
        {
            Debug.LogWarning($"[MosaicCover] LoadImage failed: {src}");
            Object.DestroyImmediate(tex);
            return false;
        }
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        // Import設定に関係なく確実にReadable化
        var readableTex = MakeReadable(tex);
        if (readableTex != tex)
        {
            Object.DestroyImmediate(tex);
            tex = readableTex;
        }

        try
        {
            // 平均色で cells x cells のモザイクを作る
            var mosaic = BuildMosaic(tex, gridCells);

            // 仕上げ用に拡大（ピクセル感を残すため Point）
            var outTex = UpscaleNearest(mosaic, outputSize, outputSize);

            // PNGで保存
            var png = outTex.EncodeToPNG();
            File.WriteAllBytes(outPath, png);

            if (verboseLog) Debug.Log($"[MosaicCover] wrote: {outPath}");

            Object.DestroyImmediate(mosaic);
            Object.DestroyImmediate(outTex);
            Object.DestroyImmediate(tex);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MosaicCover] generate failed in {dir}: {ex.Message}");
            Object.DestroyImmediate(tex);
            return false;
        }

        // Debug.Log($"[MosaicCover] wrote: {outPath}");
        return true;
    }

    /// <summary>
    /// 画像を cells x cells に分割し、各ブロックの平均色で塗った小さなテクスチャを返す
    /// </summary>
    private Texture2D BuildMosaic(Texture2D src, int cells)
    {
        cells = Mathf.Max(2, cells);

        int w = src.width;
        int h = src.height;

        // 念のためReadable保証
        if (!src.isReadable)
        {
            src = MakeReadable(src);
        }

        // 各ブロックの境界を「均等割り」(端は余りを吸収)
        int[] xCuts = new int[cells + 1];
        int[] yCuts = new int[cells + 1];
        for (int i = 0; i <= cells; i++)
        {
            xCuts[i] = Mathf.RoundToInt((float)i / cells * w);
            yCuts[i] = Mathf.RoundToInt((float)i / cells * h);
        }

        var small = new Texture2D(cells, cells, TextureFormat.RGBA32, false);
        small.wrapMode = TextureWrapMode.Clamp;
        small.filterMode = FilterMode.Point;

        // GetPixels32 一括の方が速い
        var srcPixels = src.GetPixels32();

        for (int cy = 0; cy < cells; cy++)
        {
            int y0 = yCuts[cy];
            int y1 = yCuts[cy + 1];

            for (int cx = 0; cx < cells; cx++)
            {
                int x0 = xCuts[cx];
                int x1 = xCuts[cx + 1];

                long r = 0, g = 0, b = 0, a = 0;
                long count = 0;

                for (int y = y0; y < y1; y++)
                {
                    int row = y * w;
                    for (int x = x0; x < x1; x++)
                    {
                        var c = srcPixels[row + x];
                        r += c.r; g += c.g; b += c.b; a += c.a;
                        count++;
                    }
                }

                if (count == 0) count = 1;
                byte R = (byte)(r / count);
                byte G = (byte)(g / count);
                byte B = (byte)(b / count);
                byte A = (byte)(a / count);

                small.SetPixel(cx, cells - 1 - cy, new Color32(R, G, B, A));
            }
        }

        // keep readable because UpscaleNearest calls GetPixels32()
        small.Apply(false, false);
        return small;
    }

    /// <summary>
    /// 最近傍で拡大（ピクセル感を保持）
    /// </summary>
    private Texture2D UpscaleNearest(Texture2D src, int outW, int outH)
    {
        if (!src.isReadable)
        {
            src = MakeReadable(src);
        }

        var outTex = new Texture2D(outW, outH, TextureFormat.RGBA32, false);
        outTex.filterMode = FilterMode.Point;
        outTex.wrapMode = TextureWrapMode.Clamp;

        var srcPixels = src.GetPixels32();
        int sw = src.width, sh = src.height;

        var outPixels = new Color32[outW * outH];
        for (int y = 0; y < outH; y++)
        {
            int sy = (int)((long)y * sh / outH);
            for (int x = 0; x < outW; x++)
            {
                int sx = (int)((long)x * sw / outW);
                outPixels[y * outW + x] = srcPixels[sy * sw + sx];
            }
        }

        outTex.SetPixels32(outPixels);
        outTex.Apply(false, true);
        return outTex;
    }

    /// <summary>
    /// 強制的にReadable化。Import設定に依存しないでピクセルアクセス可能なTexture2Dを返す。
    /// </summary>
    private Texture2D MakeReadable(Texture2D src)
    {
        if (src == null) return null;
        try
        {
            if (src.isReadable) return src;
        }
        catch { /* some Unity versions may throw if texture is invalid */ }

        // Blit -> ReadPixels でReadableなコピーを作る
        int w = Mathf.Max(1, src.width);
        int h = Mathf.Max(1, src.height);
        var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        rt.Create();

        var prev = RenderTexture.active;
        try
        {
            Graphics.Blit(src, rt);
            var readable = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            RenderTexture.active = rt;
            readable.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            readable.Apply(false, false);
            return readable;
        }
        finally
        {
            RenderTexture.active = prev;
            rt.Release();
        }
    }
}
#endif