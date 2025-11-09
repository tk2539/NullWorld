using UnityEngine;
using System.IO;
using System.IO.Compression;

public class ZipChartImporter : MonoBehaviour
{
    public void OnClickImportZip()
    {
        // OS標準ダイアログを使用してzipファイルを選択
        NativeFileDialog.OpenFile("Import Chart Zip", "zip", path =>
        {
            if (!string.IsNullOrEmpty(path))
            {
                ImportZip(path);
            }
        });
    }

    void ImportZip(string zipPath)
    {
        string songName = Path.GetFileNameWithoutExtension(zipPath);

        // 一時展開フォルダ
        string tempDir = Path.Combine(Application.persistentDataPath, "ImportTemp", songName);
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        ZipFile.ExtractToDirectory(zipPath, tempDir);

        // ── zipの中身を正規化：フォルダ1個しかない場合はその中へ潜る
        string srcDir = tempDir;
        string[] rootFiles = Directory.GetFiles(tempDir);
        string[] rootDirs = Directory.GetDirectories(tempDir);

        if (rootFiles.Length == 0 && rootDirs.Length == 1)
        {
            // MySong.zip/MySong/(song.mp3 など)
            srcDir = rootDirs[0];
        }

        // zipの中に曲名と同名のフォルダがあり、その中にさらに同名フォルダがある場合を防ぐ
        string innerDir = Path.Combine(srcDir, songName);
        if (Directory.Exists(innerDir))
        {
            srcDir = innerDir;
        }

        // Editor中はStreamingAssets、それ以外はpersistentDataPathへ保存
        string chartsRoot = ChartPaths.ChartsPersistentRoot;
        if (!Directory.Exists(chartsRoot)) Directory.CreateDirectory(chartsRoot);

        if (!Directory.Exists(chartsRoot)) Directory.CreateDirectory(chartsRoot);

        // 曲名フォルダをターゲットとして作成
        string targetDir = Path.Combine(chartsRoot, songName);
        if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
        DirectoryCopy(srcDir, targetDir, true);

        Debug.Log($"[Importer] Imported {songName} to {targetDir}");

#if UNITY_EDITOR
        // Unityエディタで即時反映
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    void DirectoryCopy(string sourceDir, string destDir, bool copySubDirs)
    {
        DirectoryInfo dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) return;

        DirectoryInfo[] dirs = dir.GetDirectories();
        Directory.CreateDirectory(destDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string temppath = Path.Combine(destDir, file.Name);
            file.CopyTo(temppath, true);
        }

        if (copySubDirs)
        {
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDir, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath, copySubDirs);
            }
        }
    }
}