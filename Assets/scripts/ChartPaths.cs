using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ChartPaths
{
    public static string ChartsPersistentRoot =>
        Path.Combine(Application.persistentDataPath, "charts");

    public static string ChartsStreamingRoot =>
        Path.Combine(Application.streamingAssetsPath, "charts");

    // 両方のルートから曲フォルダを列挙（重複は persistent 優先）
    public static List<string> EnumerateAllSongDirs()
    {
        var list = new List<string>();
        var seen = new HashSet<string>(); // フォルダ名重複を排除

        void addFrom(string root)
        {
            if (!Directory.Exists(root)) return;
            foreach (var dir in Directory.GetDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (seen.Add(name)) list.Add(dir); // 初見のみ追加
            }
        }

        addFrom(ChartsPersistentRoot); // 先にユーザー追加（優先）
        addFrom(ChartsStreamingRoot);  // 次に内蔵

        return list;
    }

    // フォルダ名から実体ディレクトリを解決（persistent優先）
    public static string ResolveSongDir(string folderName)
    {
        string p = Path.Combine(ChartsPersistentRoot, folderName);
        if (Directory.Exists(p)) return p;
        string s = Path.Combine(ChartsStreamingRoot, folderName);
        if (Directory.Exists(s)) return s;
        return null;
    }

    public static string ResolveAudioPath(string folderName)
    {
        var dir = ResolveSongDir(folderName);
        if (string.IsNullOrEmpty(dir)) return null;
        var mp3 = Path.Combine(dir, "song.mp3");
        var ogg = Path.Combine(dir, "song.ogg");
        var wav = Path.Combine(dir, "song.wav");
        if (File.Exists(mp3)) return mp3;
        if (File.Exists(ogg)) return ogg;
        if (File.Exists(wav)) return wav;
        return null;
    }

    public static string ResolveCoverPath(string folderName)
    {
        var dir = ResolveSongDir(folderName);
        if (string.IsNullOrEmpty(dir)) return null;
        var png = Path.Combine(dir, "cover.png");
        var jpg = Path.Combine(dir, "cover.jpg");
        if (File.Exists(png)) return png;
        if (File.Exists(jpg)) return jpg;
        return null;
    }

    public static string ResolveMetadataPath(string folderName)
    {
        var dir = ResolveSongDir(folderName);
        if (string.IsNullOrEmpty(dir)) return null;
        var meta = Path.Combine(dir, "metadata.json");
        return File.Exists(meta) ? meta : null;
    }

    public static string ResolveChartJsonPath(string folderName, string difficultyJsonName)
    {
        var dir = ResolveSongDir(folderName);
        if (string.IsNullOrEmpty(dir)) return null;
        var p = Path.Combine(dir, difficultyJsonName);
        return File.Exists(p) ? p : null;
    }
}