using System.IO;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class SongDetailUI : MonoBehaviour
{
    [Header("UI refs")]
    public RawImage bigCover;
    public TMP_Text titleText;
    public TMP_Text subtitleText;
    public Button playButton;

    [Header("Difficulty Buttons")]
    public Button easyBtn;
    public Button normalBtn;
    public Button hardBtn;
    public Button expertBtn;
    public Button masterBtn;

    [Header("Cached data")]
    private Meta _meta;
    string folder;
    string selectedChartFile;

    [Header("Level number labels")]
    public TMP_Text easyLv;
    public TMP_Text normalLv;
    public TMP_Text hardLv;
    public TMP_Text expertLv;
    public TMP_Text masterLv;

    void Awake()
    {
        if (playButton)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnClickPlay);
        }
        ClearUI();
    }

    void ClearUI()
    {
        if (bigCover) bigCover.texture = null;
        if (titleText) titleText.text = "";
        if (subtitleText) subtitleText.text = "";
        selectedChartFile = null;
        SetButtonsInteractable(false, false, false, false, false);
        HighlightSelected(null);
    }

    public void SetData(Texture2D coverTex, string folderName)
    {
        folder = folderName;
        if (bigCover) bigCover.texture = coverTex;
        var meta = LoadMeta(folderName);
        _meta = meta;
        if (titleText) titleText.text = string.IsNullOrEmpty(meta.title) ? folderName : meta.title;
        if (subtitleText) subtitleText.text = !string.IsNullOrEmpty(meta.subtitle)
                                            ? meta.subtitle
                                            : (!string.IsNullOrEmpty(meta.artist) ? meta.artist : "");

        RefreshDifficultyButtons();
        RefreshLevelNumbers();
    }

    [System.Serializable]
    class Meta
    {
        [System.Serializable]
        public class Levels
        {
            public int easy = -1;
            public int normal = -1;
            public int hard = -1;
            public int expert = -1;
            public int master = -1;

            public bool HasEasy => easy >= 0;
            public bool HasNormal => normal >= 0;
            public bool HasHard => hard >= 0;
            public bool HasExpert => expert >= 0;
            public bool HasMaster => master >= 0;
        }

        public string title;
        public string artist;
        public string subtitle;
        public float bpm;
        public float offset;
        public Levels levels = new Levels();
        public Levels difficulties = null;

        public static bool HasAny(Levels l){ return l != null && (l.easy>=0 || l.normal>=0 || l.hard>=0 || l.expert>=0 || l.master>=0); }
    }

    Meta LoadMeta(string folderName)
    {
        try
        {
            string metaPath = ChartPaths.ResolveMetadataPath(folderName);
            if (!string.IsNullOrEmpty(metaPath) && File.Exists(metaPath))
            {
                var json = File.ReadAllText(metaPath);
                var m = JsonUtility.FromJson<Meta>(json);
                var meta = m ?? new Meta();
                if (!Meta.HasAny(meta.levels) && Meta.HasAny(meta.difficulties))
                {
                    meta.levels = meta.difficulties;
                }
                return meta;
            }
        }
        catch { /* 無視 */ }
        return new Meta();
    }

    string FindChartFileInDir(string dir, string baseName)
    {
        if (!Directory.Exists(dir)) { Debug.LogWarning($"[SongDetailUI] dir not found: {dir}"); return null; }
        try
        {
            var files = Directory.GetFiles(dir);
            string wantJson = baseName + ".json";
            string wantSus  = baseName + ".sus";

            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                if (string.Equals(name, wantJson, StringComparison.OrdinalIgnoreCase)) return name;
            }
            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                if (string.Equals(name, wantSus, StringComparison.OrdinalIgnoreCase)) return name;
            }
            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                if ((name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".sus", StringComparison.OrdinalIgnoreCase))
                    && name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongDetailUI] FindChartFileInDir error: {ex.Message}\n dir={dir}");
        }
        return null;
    }

    string FindAnyChartInDir(string dir)
    {
        if (!Directory.Exists(dir)) return null;
        try
        {
            var files = Directory.GetFiles(dir);
            foreach (var f in files)
                if (f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    return Path.GetFileName(f);
            foreach (var f in files)
                if (f.EndsWith(".sus", StringComparison.OrdinalIgnoreCase))
                    return Path.GetFileName(f);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SongDetailUI] FindAnyChartInDir error: {ex.Message}\n dir={dir}");
        }
        return null;
    }

    bool HasAnyLevelDefined(Meta m)
    {
        return Meta.HasAny(m?.levels) || Meta.HasAny(m?.difficulties);
    }

    void RefreshDifficultyButtons()
    {
        string dir = ChartPaths.ResolveSongDir(folder);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            Debug.LogWarning($"[SongDetailUI] song dir not found: {folder}");
            SetButtonsInteractable(false, false, false, false, false);
            return;
        }

        string eName = FindChartFileInDir(dir, "easy");
        string nName = FindChartFileInDir(dir, "normal");
        string hName = FindChartFileInDir(dir, "hard");
        string xName = FindChartFileInDir(dir, "expert");
        string mName = FindChartFileInDir(dir, "master");

        Debug.Log($"[SongDetailUI] avail in {folder}  E={eName}  N={nName}  H={hName}  X={xName}  M={mName}");

        bool levelsDefined = HasAnyLevelDefined(_meta);
        bool allowE = !levelsDefined || (_meta?.levels?.HasEasy ?? false);
        bool allowN = !levelsDefined || (_meta?.levels?.HasNormal ?? false);
        bool allowH = !levelsDefined || (_meta?.levels?.HasHard ?? false);
        bool allowX = !levelsDefined || (_meta?.levels?.HasExpert ?? false);
        bool allowM = !levelsDefined || (_meta?.levels?.HasMaster ?? false);

        bool e = (eName != null) && allowE;
        bool n = (nName != null) && allowN;
        bool h = (hName != null) && allowH;
        bool x = (xName != null) && allowX;
        bool m = (mName != null) && allowM;

        SetButtonsInteractable(e, n, h, x, m);

        void Bind(Button b, string fname)
        {
            if (!b) return;
            b.onClick.RemoveAllListeners();
            if (!string.IsNullOrEmpty(fname))
                b.onClick.AddListener(() => SetDifficulty(fname));
        }
        Bind(easyBtn, eName);
        Bind(normalBtn, nName);
        Bind(hardBtn, hName);
        Bind(expertBtn, xName);
        Bind(masterBtn, mName);

        string fallback = null;
        if (m) fallback = mName;
        else if (x) fallback = xName;
        else if (h) fallback = hName;
        else if (n) fallback = nName;
        else if (e) fallback = eName;

        if (fallback == null)
        {
            var chartJson = Path.Combine(dir, "chart.json");
            var indexJson = Path.Combine(dir, "index.json");
            if (File.Exists(chartJson)) fallback = "chart.json";
            else if (File.Exists(indexJson)) fallback = "index.json";
            else fallback = FindAnyChartInDir(dir);
        }

        SetDifficulty(fallback);
    }

    void RefreshLevelNumbers()
    {
        SetNum(easyLv, _meta?.levels?.easy ?? -1);
        SetNum(normalLv, _meta?.levels?.normal ?? -1);
        SetNum(hardLv, _meta?.levels?.hard ?? -1);
        SetNum(expertLv, _meta?.levels?.expert ?? -1);
        SetNum(masterLv, _meta?.levels?.master ?? -1);

        void SetNum(TMP_Text t, int v)
        {
            if (!t) return;
            if (v >= 0)
            {
                t.text = v.ToString();
                t.color = Color.black;
            }
            else
            {
                t.text = "—";
                t.color = new Color(1f, 1f, 1f, 0.35f);
            }
        }
    }

    void SetButtonsInteractable(bool e, bool n, bool h, bool x, bool m)
    {
        void Apply(Button b, bool on)
        {
            if (!b) return;
            b.interactable = on;
            var cg = b.GetComponent<CanvasGroup>();
            if (!cg) cg = b.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = on ? 1f : 0.5f;
        }
        Apply(easyBtn, e);
        Apply(normalBtn, n);
        Apply(hardBtn, h);
        Apply(expertBtn, x);
        Apply(masterBtn, m);
    }

    void SetDifficulty(string chartFile)
    {
        selectedChartFile = chartFile;
        HighlightSelected(chartFile);

        if (!string.IsNullOrEmpty(chartFile))
            Debug.Log($"[SongDetailUI] Select difficulty: {folder}/{chartFile}");
        else
            Debug.LogWarning($"[SongDetailUI] No chart file in folder: {folder}");
    }

    void HighlightSelected(string chartFile)
    {
        bool IsSelected(string baseName)
        {
            return chartFile == baseName + ".json" || chartFile == baseName + ".sus";
        }
        void SetOutline(Button b, bool selected)
        {
            if (!b) return;
            var outline = b.GetComponent<Outline>();
            if (selected)
            {
                if (!outline) outline = b.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.cyan;
                outline.effectDistance = new Vector2(3, 3);
            }
            else
            {
                if (outline) Destroy(outline);
            }
        }
        SetOutline(easyBtn, IsSelected("easy"));
        SetOutline(normalBtn, IsSelected("normal"));
        SetOutline(hardBtn, IsSelected("hard"));
        SetOutline(expertBtn, IsSelected("expert"));
        SetOutline(masterBtn, IsSelected("master"));
    }

    void OnClickPlay()
    {
        if (string.IsNullOrEmpty(folder))
        {
            Debug.LogWarning("[SongDetailUI] 曲が選択されていません。");
            return;
        }

        string dir = ChartPaths.ResolveSongDir(folder);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            Debug.LogWarning($"[SongDetailUI] 曲フォルダが見つかりません: {folder}");
            return;
        }

        string finalChart = selectedChartFile;
        bool existsExact(string f) => !string.IsNullOrEmpty(f) && File.Exists(Path.Combine(dir, f));

        if (!existsExact(finalChart))
        {
            string[] order = { "master", "expert", "hard", "normal", "easy" };
            foreach (var baseName in order)
            {
                var found = FindChartFileInDir(dir, baseName);
                if (!string.IsNullOrEmpty(found)) { finalChart = found; break; }
            }

            if (!existsExact(finalChart))
            {
                if (existsExact("chart.json")) finalChart = "chart.json";
                else if (existsExact("index.json")) finalChart = "index.json";
                else finalChart = FindAnyChartInDir(dir);
            }
        }

        if (string.IsNullOrEmpty(finalChart))
        {
            Debug.LogWarning($"[SongDetailUI] この曲フォルダに譜面ファイルが見つかりません: {folder}");
            return;
        }

        string audioFilePath = ChartPaths.ResolveAudioPath(folder);
        if (string.IsNullOrEmpty(audioFilePath))
        {
            Debug.LogWarning($"[SongDetailUI] 音源ファイルが見つかりません: {folder}");
        }
        else if (MusicPlayer.Instance != null)
        {
            Debug.Log($"[SongDetailUI] Queue audio: {audioFilePath}");
            MusicPlayer.Instance.QueueForGame(audioFilePath, loop: false, startTime: 0f);
        }

        PlayerPrefs.SetString("SongFolder", folder);
        PlayerPrefs.SetString("ChartFile", finalChart);
        PlayerPrefs.Save();

        Debug.Log($"[SongDetailUI] Play: {folder}/{finalChart}");
        SceneManager.LoadScene("game");
    }
}