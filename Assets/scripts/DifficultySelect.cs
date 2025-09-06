using UnityEngine;
using System.IO;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Collections.Generic;

public class DifficultySelect : MonoBehaviour
{
    [Header("UI")]
    public GameObject buttonPrefab;      // 難易度ボタンのPrefab（中に TMP_Text or Text を想定）
    public Transform buttonParent;       // ボタンを並べる親

    [Header("Search Options")]
    [Tooltip("譜面JSONを探すサブフォルダ（空文字は直下）。必要に応じて増やしてOK")]
    public string[] jsonSearchSubdirs = new[] { "", "charts" };
    [Tooltip("難易度リストから除外するJSON（拡張子なし・小文字）")]
    public string[] excludeJsonNames = new[] { "metadata" };

    [Header("Music (optional)")]
    [Tooltip("曲プレビューで探す音源の拡張子優先順")]
    public string[] musicExtensions = new[] { "*.mp3", "*.ogg", "*.wav" };

    [Header("Debug Overlay")]
    public TextMeshProUGUI debugText;    // なくてもOK。割り当てれば画面にも出す

    void Start()
    {
        // 0) 必須UIのチェック
        if (buttonPrefab == null || buttonParent == null)
        {
            LogError("buttonPrefab または buttonParent が未設定です。Inspector を確認してね。");
            return;
        }

        // 1) 曲フォルダの取得
        string folderPath = PlayerPrefs.GetString("SelectedSongFolderPath", "");
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            LogError($"曲フォルダが無効: {folderPath}");
            return;
        }
        Log($"曲フォルダ: {folderPath}");

        // 2) 可能なら曲プレビューを再生（MusicPlayer があれば）
        TryPlayMusic(folderPath);

        // 3) 譜面JSONを探索してボタン生成
        var charts = FindChartJsons(folderPath);
        if (charts.Count == 0)
        {
            LogWarn("譜面JSONが見つかりませんでした。直下 or /charts を確認してね。");
            ShowTemporaryMessage("譜面が見つかりませんでした");
            return;
        }

        int created = 0;
        foreach (var json in charts.OrderBy(p => p))
        {
            if (CreateButtonForChart(json)) created++;
        }
        Log($"生成したボタン数: {created}");
        if (created == 0)
        {
            ShowTemporaryMessage("ボタン生成に失敗しました（PrefabにTextが無い等）");
        }
    }

    // ─────────────────────────────────────────────────────────────
    void TryPlayMusic(string folderPath)
    {
        if (MusicPlayer.Instance == null)
        {
            Log("MusicPlayer が見つからないため、プレビュー再生はスキップ。");
            return;
        }

        // 直下 → charts/ の順で音源探索（必要なら配列にサブフォルダを追加）
        var searchRoots = new List<string> { folderPath, Path.Combine(folderPath, "charts") };
        foreach (var root in searchRoots.Where(Directory.Exists))
        {
            foreach (var pat in musicExtensions)
            {
                var hit = Directory.GetFiles(root, pat, SearchOption.TopDirectoryOnly)
                                   .OrderBy(p => p)
                                   .FirstOrDefault();
                if (!string.IsNullOrEmpty(hit))
                {
                    Log($"音源: {hit}");
                    MusicPlayer.Instance.PlayFromFile(hit, loop: true);
                    return; // 1個見つけたら終了
                }
            }
        }
        Log("音源ファイルが見つかりませんでした（プレビューなしで続行）。");
    }

    List<string> FindChartJsons(string folderPath)
    {
        var results = new List<string>();
        var exclude = new HashSet<string>(excludeJsonNames.Select(s => s.ToLowerInvariant()));

        foreach (var sub in jsonSearchSubdirs)
        {
            string root = string.IsNullOrEmpty(sub) ? folderPath : Path.Combine(folderPath, sub);
            if (!Directory.Exists(root)) continue;

            var jsons = Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var path in jsons)
            {
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (exclude.Contains(name)) continue;
                results.Add(path);
            }
            Log($"探索: {root} → {jsons.Length}件（除外後 {results.Count}件累計）");
        }

        // 重複除去
        return results.Distinct().ToList();
    }

    bool CreateButtonForChart(string jsonPath)
    {
        try
        {
            var go = Instantiate(buttonPrefab, buttonParent);
            if (!go.activeSelf) go.SetActive(true);
            go.name = $"Btn_{Path.GetFileNameWithoutExtension(jsonPath)}";

            // 表示名はファイル名（拡張子なし）を整形
            string label = ToTitleCase(Path.GetFileNameWithoutExtension(jsonPath));

            // TMP or Text を探す
            var tmp = go.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = label;
            var txt = go.GetComponentInChildren<Text>();
            if (txt != null) txt.text = label;
            if (tmp == null && txt == null)
            {
                LogWarn($"ボタンに Text/TMP_Text が見つかりません: {go.name}");
            }

            // Button を取得（無ければ付ける）
            var button = go.GetComponentInChildren<Button>();
            if (button == null) button = go.AddComponent<Button>();

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnDifficultySelected(jsonPath));

            Log($"ボタン生成: {label} ({jsonPath})");
            return true;
        }
        catch (System.Exception ex)
        {
            LogError($"ボタン生成失敗: {jsonPath}\n{ex}");
            return false;
        }
    }

    string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var tokens = s.Replace('-', ' ').Replace('_', ' ').Split(' ');
        for (int i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (t.Length == 0) continue;
            tokens[i] = char.ToUpperInvariant(t[0]) + (t.Length > 1 ? t.Substring(1).ToLowerInvariant() : "");
        }
        return string.Join(" ", tokens);
    }

    void OnDifficultySelected(string jsonPath)
    {
        Debug.Log("選択された難易度: " + jsonPath);
        PlayerPrefs.SetString("SelectedChartPath", jsonPath);
        PlayerPrefs.Save();

        // 再生は止めない → game シーンでも鳴り続ける
        SceneManager.LoadScene("game");
    }

    // ─────────────────────────────────────────────────────────────
    // ログ & 画面表示（任意）
    void Log(string msg)    { Debug.Log($"[DifficultySelect] {msg}"); AppendDebug(msg); }
    void LogWarn(string msg){ Debug.LogWarning($"[DifficultySelect] {msg}"); AppendDebug("<color=yellow>" + msg + "</color>"); }
    void LogError(string msg){ Debug.LogError($"[DifficultySelect] {msg}"); AppendDebug("<color=red>" + msg + "</color>"); }

    void AppendDebug(string line)
    {
        if (debugText == null) return;
        debugText.text += (debugText.text.Length > 0 ? "\n" : "") + line;
    }

    void ShowTemporaryMessage(string msg)
    {
        if (debugText == null) return;
        debugText.gameObject.SetActive(true);
        debugText.text = msg;
    }
}