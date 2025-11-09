using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

public static class NativeFileDialog
{
    /// <summary>
    /// OSネイティブのファイル選択ダイアログを表示（単一ファイル）。
    /// title: ダイアログのタイトル（macのみ反映）
    /// ext:   例 "zip" / "json" / "*"。複数OK: "zip;json"
    /// onDone(path): キャンセル時は null
    /// </summary>
    public static void OpenFile(string title, string ext, Action<string> onDone)
    {
#if UNITY_EDITOR
        // エディタでは標準APIが最も確実
        string filter = SanitizeExt(ext);
        string path = UnityEditor.EditorUtility.OpenFilePanel(string.IsNullOrEmpty(title) ? "Open File" : title, "", filter);
        onDone?.Invoke(string.IsNullOrEmpty(path) ? null : path);
#elif UNITY_STANDALONE_WIN
        OpenFile_Windows(ext, onDone);
#elif UNITY_STANDALONE_OSX
        OpenFile_macOS(title, ext, onDone);
#else
        Debug.LogWarning("[NativeFileDialog] Unsupported platform.");
        onDone?.Invoke(null);
#endif
    }

    static string SanitizeExt(string ext)
    {
        if (string.IsNullOrEmpty(ext)) return "*";
        ext = ext.Replace(".", "").Replace(" ", "");
        return ext;
    }

#if UNITY_STANDALONE_OSX
    static void OpenFile_macOS(string title, string ext, Action<string> onDone)
    {
        // AppleScript で NSOpenPanel を呼ぶ
        // 拡張子フィルタ: {"zip","json"} のように渡す
        string[] exts = SanitizeExt(ext).Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (exts.Length == 0) exts = new[] { "*" };

        string typeList = (exts.Length == 1 && exts[0] == "*")
            ? "{}"
            : "{" + string.Join(",", Array.ConvertAll(exts, e => $"\"{e}\"")) + "}";

        string script =
            "set _types to " + typeList + "\n" +
            "set _prompt to " + AppleScriptQuote(string.IsNullOrEmpty(title) ? "Open File" : title) + "\n" +
            "set fp to choose file with prompt _prompt of type _types\n" +
            "POSIX path of fp";

        RunProcess("/usr/bin/osascript", "-e " + ShellQuote(script), out string stdout, out string stderr, out int code);
        if (code == 0)
        {
            string path = stdout.Trim();
            onDone?.Invoke(File.Exists(path) ? path : null);
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[NativeFileDialog][macOS] osascript error: {stderr}");
            onDone?.Invoke(null);
        }
    }

    static string AppleScriptQuote(string s)
    {
        // AppleScript のクォートエスケープ
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
#endif

#if UNITY_STANDALONE_WIN
    static void OpenFile_Windows(string ext, Action<string> onDone)
    {
        // PowerShell で WinForms の OpenFileDialog を起動
        string[] exts = SanitizeExt(ext).Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (exts.Length == 0) exts = new[] { "*" };

        string filter = (exts.Length == 1 && exts[0] == "*")
            ? "All Files (*.*)|*.*"
            : $"{string.Join("; ", Array.ConvertAll(exts, e => $"*.{e}"))}|" +
              $"{string.Join("; ", Array.ConvertAll(exts, e => $"*.{e}"))}";

        // 改行禁止。-STA/-MTA なしでもOKだが、Add-Type が必要
        string ps = $@"
Add-Type -AssemblyName System.Windows.Forms;
$ofd = New-Object System.Windows.Forms.OpenFileDialog;
$ofd.Filter = '{filter}';
$ofd.Multiselect = $false;
if ($ofd.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {{
  Write-Output $ofd.FileName;
}}";

        RunProcess("powershell", $"-NoProfile -NonInteractive -Command {ShellQuote(ps)}",
                   out string stdout, out string stderr, out int code);

        if (code == 0)
        {
            string path = stdout.Trim();
            onDone?.Invoke(File.Exists(path) ? path : null);
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[NativeFileDialog][Win] powershell error: {stderr}");
            onDone?.Invoke(null);
        }
    }
#endif

    static string ShellQuote(string s)
    {
        // 外部プロセス引数用の安全なクォート
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // PowerShell は ' で囲み、内部の ' は '' に
        return "'" + s.Replace("'", "''") + "'";
#else
        // /bin/sh は ' で囲み、内部の ' は '\'' に
        return "'" + s.Replace("'", "'\\''") + "'";
#endif
    }

    static void RunProcess(string fileName, string arguments, out string stdout, out string stderr, out int exitCode)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        try
        {
            using var p = Process.Start(psi);
            stdout = p.StandardOutput.ReadToEnd();
            stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            exitCode = p.ExitCode;
        }
        catch (Exception e)
        {
            stdout = ""; stderr = e.Message; exitCode = -1;
        }
    }
}