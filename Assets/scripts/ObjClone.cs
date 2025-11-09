using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

public class ObjClone : MonoBehaviour
{
    [Header("Timing (BPM)")]
    [Tooltip("metadata.json の bpm を使って、time/hold を拍(beat)として解釈する")]
    public bool timesAreBeats = true;
    [Tooltip("metadata.json から読み取った BPM。0 以下なら従来(秒/Z)解釈")]
    public float bpm = 0f;

    [Header("Visual Speed Scale (方向別の見た目調整)")]
    [Tooltip("順走(速度>=0)の見た目倍率")] public float forwardSpeedScale = 1f;
    [Tooltip("逆走(速度<0)の見た目倍率")] public float reverseSpeedScale = 1f / 6f; // 逆走は1/6の見た目速度に

    [Header("Base Speed (ノーツにspeedが無いときの既定値)")]
    [Tooltip("見た目の基準スピード（Z/分, 正の値）。逆走は区間/方向倍率で制御する想定")]
    public float baseSpeedAbs = 1200f;

    // === Player Speed Multiplier ===
    [Header("Player Note Speed Multiplier")]
    [Tooltip("6.0〜12.0まで設定可能。10.0がデフォルト。プレイヤー設定から変更される。")]
    public static float userSpeedMultiplier = 1.0f; // 10.0相当（=1.0）

    [System.Serializable]
    public struct SpeedScaleMarker
    {
        public float beat;     // この拍以降に適用（拍基準）
        public float forward;  // 順走倍率（<=0 は無視）
        public float reverse;  // 逆走倍率（<=0 は無視）
    }

    /// <summary>
    /// メタデータ: BPM, speedScaleMarkers, rootoffsetMs（譜面全体の開始オフセット[ms]）
    /// </summary>
    [System.Serializable]
    class Metadata { public float bpm; public SpeedScaleMarker[] speedScaleMarkers; public float rootoffsetMs; }

    // metadata.json から読み込む区間ソフランのマーカー
    private List<SpeedScaleMarker> _markers = new List<SpeedScaleMarker>();
    // クローン順に処理するための実行リスト
    private readonly List<Transform> _moveOrder = new List<Transform>();
    private float _lastPruneTime = 0f;

    // metadata.rootoffsetMs を秒にした値（+で全体を後ろへ、-で前へ）
    public static float GlobalRootOffsetSec { get; private set; } = 0f;

    bool UseBeats => timesAreBeats && bpm > 1e-3f;

    float SecPerBeat() => (bpm > 1e-3f) ? 60f / bpm : 1f;
    float BeatsToSec(float beats) => beats * SecPerBeat();
    float BeatsToZ(float beats, float speedZPerMin)
    {
        // appearZ = beats * speed / bpm  （speed: Z/min）
        // 速度の符号を保持したまま Z 方向の距離を算出
        if (bpm <= 1e-3f) return beats; // フォールバック
        return beats * (speedZPerMin / bpm);
    }

    // 指定拍区間の見た目距離Zを、区間ソフラン(_markers)を考慮して積分計算
    // sign: 順走=+1, 逆走=-1 / rawSpeedAbs: ノーツの絶対基準速度(Z/分)
    float ZDistanceBetweenBeats(float startBeatAbs, float endBeatAbs, int sign, float rawSpeedAbs)
    {
        if (!UseBeats || bpm <= 1e-3f) return 0f;
        if (Mathf.Approximately(startBeatAbs, endBeatAbs)) return 0f;

        float a = Mathf.Min(startBeatAbs, endBeatAbs);
        float b = Mathf.Max(startBeatAbs, endBeatAbs);
        float sum = 0f;

        // 区間境界リスト（a..b の間のマーカー拍）
        List<float> edges = new List<float>();
        edges.Add(a);
        for (int i = 0; i < _markers.Count; i++)
        {
            float mb = _markers[i].beat;
            if (mb > a + 1e-6f && mb < b - 1e-6f) edges.Add(mb);
        }
        edges.Add(b);
        edges.Sort();

        for (int i = 0; i < edges.Count - 1; i++)
        {
            float segStart = edges[i];
            float segEnd = edges[i + 1];
            float segBeats = segEnd - segStart;
            if (segBeats <= 1e-6f) continue;

            // 区間開始拍の倍率を採用
            float dirScale = GetDirScaleForBeat(segStart, sign >= 0 ? 1 : -1);
            float speedPerMin = Mathf.Abs(rawSpeedAbs) * Mathf.Max(0f, dirScale) * userSpeedMultiplier;
            sum += BeatsToZ(segBeats, speedPerMin);
        }

        float sgn = (endBeatAbs >= startBeatAbs ? 1f : -1f) * (sign >= 0 ? 1f : -1f);
        return sgn * sum;
    }

    [Header("Prefabs")]
    public GameObject normalNotesPrefab;
    public GameObject criticalNotesPrefab;
    public GameObject flickNotesPrefab;

    [Header("Guide Prefab (判定なしのガイド用)")]
    [Tooltip("bar/start-notes/end-notes を子に持つ見た目専用のノーツ。透明度100/255推奨")] public GameObject guideNotesPrefab;

    [Header("Long Note Parent (bar/start-notes/end-notes を子に持つ)")]
    public GameObject longnoteParent;

    // === Active制御ユーティリティ ===
    bool IsSceneObject(GameObject go) => go != null && go.scene.IsValid();
    void DeactivateIfSceneObject(GameObject go)
    {
        if (IsSceneObject(go) && go.activeSelf) go.SetActive(false);
    }
    void ActivateRecursively(GameObject go)
    {
        if (go == null) return;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.SetActive(true);
    }
    void DeactivateTemplatePrefabsInScene()
    {
        // シーンに置いてあるテンプレ（デバッグ用の見本）だけを非表示にする
        DeactivateIfSceneObject(normalNotesPrefab);
        DeactivateIfSceneObject(criticalNotesPrefab);
        DeactivateIfSceneObject(flickNotesPrefab);
        DeactivateIfSceneObject(longnoteParent);
    }

    [System.Serializable]
    public class NoteData
    {
        public int lane;        // 1..12（左端）
        public int width = 1;   // 1..12
        public float time;      // 【UseBeats=true】拍(beat)。【false】出発Z
        public float hold;      // 【UseBeats=true】拍(beat)。【false】Z長
        public int speed;       // Z/分（符号付き：負で逆走）
        public string type;     // "normal" | "critical" | "flick" | "long" | "guide"
        public bool fake;       // ダミー
        public float arrival;   // 秒（省略可）。無い/0なら time*60/speed で算出（符号保持）
    }
    [System.Serializable] public class NoteDataArray { public List<NoteData> array; }
    [System.Serializable] public class NoteDataNotes { public List<NoteData> notes; }
    [System.Serializable] public class NoteDataRoot { public List<NoteData> array; public List<NoteData> notes; }

    // 全ノーツの基準開始時刻（Time.time 基準。カウントダウン無し）
    private float chartStartTimeForAllNotes;

    // ==== 中央揃えのX算出： x_center = (lane - 7) + width / 2  (lane∈1..12, width∈1..12) ====
    public static float XFromLaneWidth(int lane, int width)
    {
        return (lane - 7f) + 0.5f * width;
    }

    // 指定拍における方向別倍率（マーカーがあれば上書き）
    float GetDirScaleForBeat(float beatAbs, int speedSign)
    {
        float fwd = Mathf.Max(0f, forwardSpeedScale);
        float rev = Mathf.Max(0f, reverseSpeedScale);
        for (int i = 0; i < _markers.Count; i++)
        {
            if (beatAbs + 1e-6f >= _markers[i].beat)
            {
                if (_markers[i].forward > 0f) fwd = _markers[i].forward;
                if (_markers[i].reverse > 0f) rev = _markers[i].reverse;
            }
            else break;
        }
        return (speedSign >= 0) ? fwd : rev;
    }

    // bar(=hold本体) を取得。優先: "bar" → "hold-notes" → "guide-notes" → 部分一致("hold")
    Transform FindBar(Transform root)
    {
        var t = root.Find("bar");
        if (t) return t;
        t = root.Find("hold-notes");
        if (t) return t;
        t = root.Find("guide-notes");
        if (t) return t;
        // ゆる検索（子孫から名前に"hold"を含む最初のTransform）
        foreach (var r in root.GetComponentsInChildren<Transform>(true))
        {
            if (r == root) continue;
            if (r.name.ToLower().Contains("hold")) return r;
        }
        return null;
    }

    void Start()
    {
        // シーン上の見本オブジェクトは最初に非表示（クローンのみ表示）
        DeactivateTemplatePrefabsInScene();
        // プレイヤー設定のノーツ速度（6.0〜12.0, 10.0基準）を読み込む
        float savedSpeed = PlayerPrefs.GetFloat("NoteSpeed", 10.0f);
        userSpeedMultiplier = Mathf.Clamp(savedSpeed, 6.0f, 12.0f) / 10.0f;

        // Selectシーンで保存した選択情報から robust にパスを解決
        string folder = PlayerPrefs.GetString("SongFolder", "all_notes");
        string chartName = PlayerPrefs.GetString("ChartFile", "");

        // ==== Resolve chart path (persistent優先 / フルパス優先) ====
        string chartPath = null;
        if (!string.IsNullOrEmpty(chartName) && (Path.IsPathRooted(chartName) || chartName.StartsWith("/")))
        {
            if (File.Exists(chartName)) chartPath = chartName;
        }
        if (chartPath == null && !string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(chartName))
        {
            chartPath = ChartPaths.ResolveChartJsonPath(folder, chartName);
        }
        if (chartPath == null && !string.IsNullOrEmpty(folder))
        {
            // fallback: 既定名の探索
            string[] order = { "master.json", "expert.json", "hard.json", "normal.json", "easy.json", "chart.json", "index.json" };
            foreach (var name in order)
            {
                var p = ChartPaths.ResolveChartJsonPath(folder, name);
                if (!string.IsNullOrEmpty(p)) { chartPath = p; break; }
            }
        }
        if (string.IsNullOrEmpty(chartPath) || !File.Exists(chartPath))
        {
            Debug.LogWarning($"[ObjClone] Chart not found. folder='{folder}' chart='{chartName}'");
            return;
        }

        // ==== Resolve metadata ====
        string metaPath = ChartPaths.ResolveMetadataPath(folder);

        // metadata.json から BPM / speedScaleMarkers を読む
        try
        {
            if (File.Exists(metaPath))
            {
                string metaText = File.ReadAllText(metaPath);
                var md = JsonUtility.FromJson<Metadata>(metaText);
                if (md != null && md.bpm > 1e-3f) bpm = md.bpm; else bpm = 0f;

                _markers.Clear();
                if (md != null && md.speedScaleMarkers != null)
                {
                    _markers.AddRange(md.speedScaleMarkers);
                    _markers.Sort((a, b) => a.beat.CompareTo(b.beat));
                }
                // rootoffsetMs: 音源と譜面の両方を同じ時間だけずらす（秒）。
                GlobalRootOffsetSec = (md != null) ? (md.rootoffsetMs / 1000f) : 0f;
            }
            else
            {
                bpm = 0f;
            }
        }
        catch { bpm = 0f; }

        // ★ 開始時刻をメタの rootoffsetMs でシフト（+なら遅らせる / -なら早める）
        chartStartTimeForAllNotes = Time.time + GlobalRootOffsetSec;

        Debug.Log($"[ObjClone] Load chart: {chartPath}\nBPM={bpm}, UseBeats={UseBeats}, chartStart={chartStartTimeForAllNotes:F3}");

        // 生成順リストを初期化
        _moveOrder.Clear();
        LoadChart(chartPath);
    }

    public void LoadChart(string path)
    {
        // リロード時にも開始時刻を固定（再生中の再ロード対策）
        if (chartStartTimeForAllNotes <= 0f) chartStartTimeForAllNotes = Time.time + GlobalRootOffsetSec;

        // 既存ノーツ消去
        foreach (Transform child in transform) Destroy(child.gameObject);
        _moveOrder.Clear();

        // スコア系リセット（任意）
        PlayerPrefs.DeleteKey("Score");
        PlayerPrefs.DeleteKey("PerfectCount");
        PlayerPrefs.DeleteKey("GreatCount");
        PlayerPrefs.DeleteKey("GoodCount");
        PlayerPrefs.DeleteKey("BadCount");
        PlayerPrefs.DeleteKey("MissCount");
        PlayerPrefs.DeleteKey("MaxCombo");
        PlayerPrefs.DeleteKey("MaxScore");
        PlayerPrefs.Save();

        // SUS か JSON かを分岐
        List<NoteData> notes = null;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".sus")
        {
            // SUS: 拍ベース前提
            timesAreBeats = true;
            notes = LoadSusChart(path);
            if (bpm <= 1e-3f) bpm = 120f; // BPM未設定なら暫定
        }
        else
        {
            // JSON 読み込み（配列 or オブジェクト(array/notes)両対応）
            string jsonText = File.ReadAllText(path);
            string trimmed = jsonText.TrimStart();
            try
            {
                if (!string.IsNullOrEmpty(trimmed) && trimmed[0] == '[')
                {
                    var wrap = JsonUtility.FromJson<NoteDataArray>("{\"array\":" + jsonText + "}");
                    notes = wrap != null ? wrap.array : null;
                }
                else
                {
                    var root = JsonUtility.FromJson<NoteDataRoot>(jsonText);
                    if (root != null)
                    {
                        if (root.array != null && root.array.Count > 0) notes = root.array;
                        else if (root.notes != null && root.notes.Count > 0) notes = root.notes;
                    }
                    if (notes == null)
                    {
                        var onlyNotes = JsonUtility.FromJson<NoteDataNotes>(jsonText);
                        if (onlyNotes != null && onlyNotes.notes != null) notes = onlyNotes.notes;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ObjClone] JSON parse error: {ex.Message}\npath={path}");
            }
        }

        if (notes == null) notes = new List<NoteData>();
        if (notes.Count == 0)
        {
            Debug.LogWarning($"[ObjClone] No notes parsed. path={path}");
        }

        foreach (var n in notes)
        {
            int lane = Mathf.Clamp(n.lane, 1, 12);
            int width = Mathf.Clamp(n.width <= 0 ? 1 : n.width, 1, 12);

            float arrivalStart;
            float arrivalEnd;
            float appearZ;     // 見た目上の出発Z（speed と bpm から算出）
            float holdZ;       // 見た目上の棒の長さ（Z方向、符号付き）

            // 1) 到達時刻（秒）は常に「正の継続時間」
            //    ★ arrival(秒) が指定されていれば最優先（UseBeatsでも秒を尊重）
            if (Mathf.Abs(n.arrival) > 1e-6f)
            {
                arrivalStart = Mathf.Abs(n.arrival);
                arrivalEnd = arrivalStart + (UseBeats ? BeatsToSec(Mathf.Abs(n.hold))
                                                        : ((n.speed != 0) ? Mathf.Abs(n.hold * 60f / n.speed)
                                                                          : Mathf.Abs(n.hold)));
            }
            else if (UseBeats)
            {
                // 拍→秒（逆走でも正の継続時間にする）
                float tBeat = Mathf.Abs(n.time);
                float holdBeat = Mathf.Abs(n.hold);
                arrivalStart = BeatsToSec(tBeat);
                arrivalEnd = arrivalStart + BeatsToSec(holdBeat);
            }
            else
            {
                // 旧: 秒/Z 解釈
                arrivalStart = (n.speed != 0) ? Mathf.Abs(n.time * 60f / n.speed) : Mathf.Abs(n.time);
                arrivalEnd = arrivalStart + ((n.speed != 0) ? Mathf.Abs(n.hold * 60f / n.speed) : Mathf.Abs(n.hold));
            }

            // ノーツの絶対拍（区間スケール選択用）
            float noteBeatAbs;
            if (UseBeats)
                noteBeatAbs = Mathf.Abs(n.time);
            else if (bpm > 1e-3f && Mathf.Abs(n.arrival) > 1e-6f)
                noteBeatAbs = Mathf.Abs(n.arrival) * bpm / 60f; // 秒→拍
            else
                noteBeatAbs = 0f;

            // ノーツに speed が無い（0）場合は基準速度を適用
            int rawSpeed = (n.speed != 0)
                           ? n.speed
                           : Mathf.RoundToInt(Mathf.Abs(baseSpeedAbs));

            float dirScale = GetDirScaleForBeat(noteBeatAbs, rawSpeed >= 0 ? 1 : -1);
            float scaledSpeed = rawSpeed * dirScale * userSpeedMultiplier; // プレイヤー設定反映
            float speedPerSec = scaledSpeed / 60f;        // Z/秒（符号付き）

            // ★ 拍モード時はマーカーを積分して見た目距離を算出（可変HSでも先端が合う）
            if (UseBeats)
            {
                float startBeatAbs = Mathf.Abs(n.time);
                float endBeatAbs = startBeatAbs + Mathf.Abs(n.hold);
                int sign = (rawSpeed >= 0) ? 1 : -1;
                appearZ = ZDistanceBetweenBeats(0f, startBeatAbs, sign, Mathf.Abs(rawSpeed));
                holdZ = ZDistanceBetweenBeats(startBeatAbs, endBeatAbs, sign, Mathf.Abs(rawSpeed));
            }
            else
            {
                appearZ = arrivalStart * speedPerSec;
                holdZ = (arrivalEnd - arrivalStart) * speedPerSec;
            }

            // 中央揃えX（式そのまま）
            float xWorld = XFromLaneWidth(lane, width);
            Vector3 basePos = new Vector3(xWorld, 0f, appearZ);

            // --- GUIDE: ignore completely for festival build ---
            if (n.type == "guide")
            {
                continue; // do not instantiate, do not register, fully skipped
            }

            if (n.type == "long")
            {
                if (!longnoteParent) { Debug.LogError("longnoteParent 未設定"); continue; }

                // 親（棒）を生成：親自体も移動させる（longroot）
                GameObject longObj = Instantiate(longnoteParent, transform);
                longObj.name = $"Long_lane{lane}_w{width}_{n.time:F3}";
                longObj.transform.position = basePos;
                // テンプレが非表示でも、クローンは強制的に可視化
                ActivateRecursively(longObj);
                // 親(=longroot)だけを移動対象に登録（子は親に追従）
                _moveOrder.Add(longObj.transform);

                // 親に NoteInfo（棒自体は判定しない）
                var rootInfo = longObj.GetComponent<NoteInfo>() ?? longObj.AddComponent<NoteInfo>();
                FillInfo(rootInfo, lane, width, n, "longroot", appearZ, arrivalStart, scaledSpeed);

                // bar の長さ調整（見た目のZスケール）
                Transform bar = FindBar(longObj.transform);
                if (bar)
                {
                    float holdSigned = holdZ;
                    float holdAbs = Mathf.Abs(holdZ);

                    // 中点へ（start=0, end=hold）
                    var lp = bar.localPosition; lp.z = holdZ * 0.5f; bar.localPosition = lp;

                    // 優先: LineRenderer → SpriteRenderer → デフォルト(Mesh等)
                    var lr = bar.GetComponent<LineRenderer>();
                    if (lr != null)
                    {
                        lr.positionCount = 2;
                        lr.useWorldSpace = false;
                        lr.SetPosition(0, Vector3.zero);
                        lr.SetPosition(1, new Vector3(0f, 0f, holdSigned));
                    }
                    else
                    {
                        var sr = bar.GetComponentInChildren<SpriteRenderer>();
                        if (sr != null)
                        {
                            if (sr.drawMode != SpriteDrawMode.Simple)
                            {
                                var size = sr.size;
                                size.y = Mathf.Abs(holdZ);
                                size.x *= (width / 3f); // 横も合わせる
                                sr.size = size;
                            }
                            else
                            {
                                var sc2 = sr.transform.localScale;
                                sc2.y = Mathf.Abs(holdZ);
                                sc2.x *= (width / 3f);   // 横も合わせる
                                sr.transform.localScale = sc2;
                            }
                        }
                        else
                        {
                            // Meshベース: Zスケールを直接設定（Cube等は1単位が基準長）
                            var sc = bar.localScale;
                            sc.z = Mathf.Abs(holdZ);
                            sc.x *= (width / 3f); // 横も合わせる
                            bar.localScale = sc;
                        }
                    }
                }

                // 子ノーツ（start/end）をローカルZに配置、判定は子で行う
                Transform start = longObj.transform.Find("start-notes");
                Transform end = longObj.transform.Find("end-notes");

                if (start)
                {
                    var p = start.localPosition; p.z = 0f; start.localPosition = p; // startはtimeの位置

                    var info = start.GetComponent<NoteInfo>() ?? start.gameObject.AddComponent<NoteInfo>();
                    FillInfo(info, lane, width, n, "longstart", appearZ, arrivalStart, scaledSpeed);

                    // 横幅を譜面の width に合わせて拡大（中心揃え）
                    var scStart = start.localScale;
                    scStart.x *= (width / 3f);
                    start.localScale = scStart;
                }
                if (end)
                {
                    var p = end.localPosition; p.z = holdZ; end.localPosition = p; // endはtime+hold（符号付き）

                    var info = end.GetComponent<NoteInfo>() ?? end.gameObject.AddComponent<NoteInfo>();
                    FillInfo(info, lane, width, n, "longend", appearZ + holdZ, arrivalEnd, scaledSpeed);

                    // 横幅を譜面の width に合わせて拡大（中心揃え）
                    var scEnd = end.localScale;
                    scEnd.x *= (width / 3f);
                    end.localScale = scEnd;
                }
            }
            /* --- GUIDE NOTES TEMPORARILY DISABLED ---
                        else if (n.type == "guide")
                        {
                            if (!guideNotesPrefab) { Debug.LogError("guideNotesPrefab 未設定"); continue; }

                            GameObject guideObj = Instantiate(guideNotesPrefab, transform);
                            guideObj.name = $"Guide_lane{lane}_w{width}_{n.time:F3}";
                            guideObj.transform.position = basePos;
                            ActivateRecursively(guideObj);

                            // 親に NoteInfo を付与（見た目移動のみ／判定なし）
                            var rootInfo = guideObj.GetComponent<NoteInfo>() ?? guideObj.AddComponent<NoteInfo>();
                            FillInfo(rootInfo, lane, width, n, "guide-root", appearZ, arrivalStart, scaledSpeed);
                            rootInfo.fake = true; // 念のためAutoMissの対象外

                            // bar の長さ調整
                            Transform bar = FindBar(guideObj.transform);
                            if (bar)
                            {
                                var lp = bar.localPosition; lp.z = holdZ * 0.5f; bar.localPosition = lp;
                                var lr = bar.GetComponent<LineRenderer>();
                                if (lr != null)
                                {
                                    lr.positionCount = 2; lr.useWorldSpace = false;
                                    lr.SetPosition(0, Vector3.zero);
                                    lr.SetPosition(1, new Vector3(0f, 0f, holdZ));
                                }
                                else
                                {
                                    var sr = bar.GetComponentInChildren<SpriteRenderer>();
                                    if (sr != null)
                                    {
                                        if (sr.drawMode != SpriteDrawMode.Simple) { var size = sr.size; size.y = Mathf.Abs(holdZ); sr.size = size; }
                                        else { var sc2 = sr.transform.localScale; sc2.y = Mathf.Abs(holdZ); sr.transform.localScale = sc2; }
                                    }
                                    else { var sc = bar.localScale; sc.z = Mathf.Abs(holdZ); bar.localScale = sc; }
                                }
                            }

                            // 子ノーツ配置（start/end）
                            Transform start = guideObj.transform.Find("start-notes");
                            Transform end   = guideObj.transform.Find("end-notes");
                            if (start)
                            {
                                var p = start.localPosition; p.z = 0f; start.localPosition = p;
                                var info = start.GetComponent<NoteInfo>() ?? start.gameObject.AddComponent<NoteInfo>();
                                FillInfo(info, lane, width, n, "guide", appearZ, arrivalStart, scaledSpeed);
                                info.fake = true; // 判定・スコア対象外
                                var scStart = start.localScale; scStart.x *= (width / 3f); start.localScale = scStart;
                            }
                            if (end)
                            {
                                var p = end.localPosition; p.z = holdZ; end.localPosition = p;
                                var info = end.GetComponent<NoteInfo>() ?? end.gameObject.AddComponent<NoteInfo>();
                                FillInfo(info, lane, width, n, "guide-end", appearZ + holdZ, arrivalEnd, scaledSpeed);
                                info.fake = true;
                                var scEnd = end.localScale; scEnd.x *= (width / 3f); end.localScale = scEnd;
                            }
                        }
            --- END GUIDE NOTES TEMPORARILY DISABLED --- */
            else
            {
                GameObject prefab =
                    n.type == "critical" ? criticalNotesPrefab :
                    n.type == "flick" ? flickNotesPrefab :
                                           normalNotesPrefab;

                if (!prefab) { Debug.LogError($"Prefab未設定: {n.type}"); continue; }

                var go = Instantiate(prefab, transform);
                go.name = $"{n.type}_lane{lane}_w{width}_{n.time:F3}";
                go.transform.position = basePos;

                // 横幅を譜面の width に合わせて拡大（中心揃え）
                var sc = go.transform.localScale;
                sc.x *= (width / 3f);
                go.transform.localScale = sc;

                ActivateRecursively(go);
                // この単体ノーツを移動対象に登録
                _moveOrder.Add(go.transform);

                var info = go.GetComponent<NoteInfo>() ?? go.AddComponent<NoteInfo>();
                FillInfo(info, lane, width, n, n.type, appearZ, arrivalStart, scaledSpeed);
            }
        }

        // MaxScore は ObjJudgement 側で算出
        var judge = FindFirstObjectByType<ObjJudgement>();
        judge?.CalcMaxScore();
    }

    void FillInfo(NoteInfo info, int lane, int width, NoteData src, string type, float appearZ, float arrivalSec, float overrideSpeed)
    {
        info.lane = lane;
        // NoteInfo に width が無い環境でも動くよう try/catch
        try { info.width = width; } catch { /* 互換のため無視 */ }

        info.appearTime = appearZ;     // 出発Z
        info.arrivalTime = arrivalSec; // 秒（符号保持）
        info.speed = overrideSpeed;    // 実効速度(Z/分)を保存（移動式にも適用）
        info.type = type;              // normal/critical/flick/longstart/longend/longroot
        info.holdValue = src.hold;
        info.fake = src.fake;
        info.startTime = chartStartTimeForAllNotes;
    }

    void Update()
    {
        // 生成順のリストに沿って移動。nullになった要素はスキップ
        for (int i = 0; i < _moveOrder.Count; i++)
        {
            var t = _moveOrder[i];
            if (!t) continue;
            MoveRecursively(t);
        }

        // 定期的に null 要素を整理（Destroy後の穴埋め）
        if (Time.time - _lastPruneTime > 1.0f)
        {
            _moveOrder.RemoveAll(tr => tr == null);
            _lastPruneTime = Time.time;
        }
    }

    void MoveRecursively(Transform t)
    {
        if (t == null || !t.gameObject) return;
        var info = t.GetComponent<NoteInfo>();
        if (info)
        {
            // longroot の子(start/end)は親で移動するのでスキップ
            if (info.type != "longroot" && t.parent && t.parent.GetComponent<NoteInfo>()?.type == "longroot")
            {
                // 子はローカルZ配置だけ（Xは親の中央揃え）
            }
            else
            {
                float elapsed = Time.time - info.startTime;                 // 秒
                float z = info.appearTime - elapsed * (info.speed / 60f);  // 符号付きspeedで移動
                float x = XFromLaneWidth(info.lane, GetWidthSafe(info));    // ★式で中央揃え
                t.position = new Vector3(x, 0f, z);

                /* --- GUIDE AUTODESPAWN TEMP DISABLED ---
                                // guide: start-notes が判定線に触れたら親ごと消す（判定なしの目安表示）
                                if (info.type == "guide")
                                {
                                    // 名前で"start-notes" を判定（ガイド階層想定）
                                    if (t.name.Contains("start-notes") && z <= 0f)
                                    {
                                        var root = t.parent ? t.parent.gameObject : t.gameObject;
                                        Destroy(root);
                                        return; // 破棄後は処理しない
                                    }
                                }
                --- END GUIDE AUTODESPAWN TEMP DISABLED --- */
            }
        }

        // 子も再帰で処理
        foreach (Transform c in t) MoveRecursively(c);
    }

    int GetWidthSafe(NoteInfo info)
    {
        try { return Mathf.Max(1, info.width); } catch { return 1; }
    }

    // === SUS Loader: normal/flick/long/critical notes ===
    List<NoteData> LoadSusChart(string path)
    {
        var notes = new List<NoteData>();
        if (!File.Exists(path)) { Debug.LogWarning($"[ObjClone] SUS not found: {path}"); return notes; }

        int B36(char ch)
        {
            if (ch >= '0' && ch <= '9') return ch - '0';
            if (ch >= 'a' && ch <= 'z') return ch - 'a' + 10;
            if (ch >= 'A' && ch <= 'Z') return ch - 'A' + 10;
            return 0;
        }

        var measLenTbl = new List<(int m, float len)> { (0, 4f) };
        float GetMeasLen(int m)
        {
            float len = 4f;
            for (int i = 0; i < measLenTbl.Count; i++)
                if (m >= measLenTbl[i].m) len = measLenTbl[i].len; else break;
            return len;
        }
        float SumMeasStart(int m)
        {
            float sum = 0f;
            for (int k = 0; k < m; k++) sum += GetMeasLen(k);
            return sum;
        }

        var rxData = new System.Text.RegularExpressions.Regex(@"^\s*#(?<meas>\d{3})(?<chan>[0-9A-Za-z]{2,3})\s*:\s*(?<data>[0-9A-Za-z]+)");
        var rx02 = new System.Text.RegularExpressions.Regex(@"^\s*#(?<meas>\d{3})02\s*:\s*(?<len>[0-9]+(?:\.[0-9]+)?)\s*$");
        var rxMEASBS = new System.Text.RegularExpressions.Regex(@"^\s*#MEASUREBS\s+(?<base>\d+)\s*$");

        int measBase = 0;

        // 長押し(2xy) を組み立てるために、チャンネルごとに出現点(拍)を集める
        var holdPoints = new Dictionary<string, List<(float beat, int lane, int width)>>();

        try
        {
            var lines = File.ReadAllLines(path);

            // pass1: 小節長
            foreach (var raw0 in lines)
            {
                var l0 = raw0;
                int p;
                if ((p = l0.IndexOf(';')) >= 0) l0 = l0.Substring(0, p);
                if ((p = l0.IndexOf("//")) >= 0) l0 = l0.Substring(0, p);
                l0 = l0.Trim();
                if (l0.Length == 0) continue;

                var mbs = rxMEASBS.Match(l0);
                if (mbs.Success)
                {
                    measBase = int.Parse(mbs.Groups["base"].Value, System.Globalization.CultureInfo.InvariantCulture);
                    continue;
                }
                var m02 = rx02.Match(l0);
                if (m02.Success)
                {
                    int m = int.Parse(m02.Groups["meas"].Value, System.Globalization.CultureInfo.InvariantCulture) + measBase;
                    float len = float.Parse(m02.Groups["len"].Value, System.Globalization.CultureInfo.InvariantCulture);
                    int idx = measLenTbl.FindIndex(t => t.m > m);
                    if (idx >= 0) measLenTbl.Insert(idx, (m, len)); else measLenTbl.Add((m, len));
                }
            }

            // pass2: ノーツ（タップ1x、フリック5x、ホールド2xy）
            measBase = 0;
            foreach (var raw in lines)
            {
                var line = raw;
                int p;
                if ((p = line.IndexOf(';')) >= 0) line = line.Substring(0, p);
                if ((p = line.IndexOf("//")) >= 0) line = line.Substring(0, p);
                line = line.Trim();
                if (line.Length == 0) continue;

                var mbs = rxMEASBS.Match(line);
                if (mbs.Success)
                {
                    measBase = int.Parse(mbs.Groups["base"].Value, System.Globalization.CultureInfo.InvariantCulture);
                    continue;
                }

                var m = rxData.Match(line);
                if (!m.Success) continue;

                int meas = int.Parse(m.Groups["meas"].Value, System.Globalization.CultureInfo.InvariantCulture) + measBase;
                string chan = m.Groups["chan"].Value; // 例: 1a / 2bc / 5f
                string data = m.Groups["data"].Value;
                int divs = data.Length / 2; // 2文字=1トークン
                if (divs <= 0) continue;

                char ch0 = chan[0];
                if (ch0 != '1' && ch0 != '2' && ch0 != '5') continue; // 今は 1x/2xy/5x のみ

                int laneLeftIdx = (chan.Length >= 2) ? B36(chan[1]) : 0;
                // SUSレーン(左端) → ゲームレーンへの補正: -2 ずらす（例: 3→1, 5→3, 9→7, 11→9）
                int lane = Mathf.Clamp(laneLeftIdx + 1 - 2, 1, 12);
                int widthFromChan = (chan.Length >= 3) ? Mathf.Clamp(B36(chan[2]), 1, 12) : -1; // 2xyのy を幅に使う

                float measLen = GetMeasLen(meas);
                float measStart = SumMeasStart(meas);

                if (ch0 == '1' || ch0 == '5')
                {
                    // タップ/フリック: トークンごとに生成
                    for (int i = 0; i < divs; i++)
                    {
                        string tok = data.Substring(i * 2, 2);
                        if (tok == "00") continue;

                        int wTok = Mathf.Clamp(B36(tok[1]), 1, 12); // 2桁目=幅
                        float frac = (float)i / divs;
                        float beat = measStart + frac * measLen;

                        string type = (ch0 == '5') ? "flick" : "normal";
                        // シンプルな critical 判定: 先頭文字が 'C' のとき
                        if (ch0 == '1' && (tok[0] == 'C' || tok[0] == 'c')) type = "critical";

                        notes.Add(new NoteData
                        {
                            lane = lane,
                            width = wTok,
                            time = beat,
                            hold = 0f,
                            speed = 0,
                            type = type,
                            fake = false,
                            arrival = 0f
                        });
                    }
                }
                else if (ch0 == '2')
                {
                    // ホールド: 同一チャンネル(2xy)で出現点を集め、後で start/end に組む
                    string key = chan; // 例: "2bc"
                    if (!holdPoints.TryGetValue(key, out var list))
                    {
                        list = new List<(float beat, int lane, int width)>();
                        holdPoints[key] = list;
                    }

                    for (int i = 0; i < divs; i++)
                    {
                        string tok = data.Substring(i * 2, 2);
                        if (tok == "00") continue;

                        int wTok = (widthFromChan > 0) ? widthFromChan : Mathf.Clamp(B36(tok[1]), 1, 12);
                        float frac = (float)i / divs;
                        float beat = measStart + frac * measLen;
                        list.Add((beat, lane, wTok));
                    }
                }
            }

            // 2xy の出現点から 長押し(long) を構築（連続2点ごとに start-end とみなす）
            foreach (var kv in holdPoints)
            {
                var pts = kv.Value;
                pts.Sort((a, b) => a.beat.CompareTo(b.beat));
                for (int i = 0; i + 1 < pts.Count; i += 2)
                {
                    var s = pts[i];
                    var e = pts[i + 1];
                    float holdBeat = Mathf.Max(0f, e.beat - s.beat);
                    notes.Add(new NoteData
                    {
                        lane = s.lane,
                        width = s.width,
                        time = s.beat,
                        hold = holdBeat,
                        speed = 0,
                        type = "long",
                        fake = false,
                        arrival = 0f
                    });
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ObjClone] SUS parse error: {ex.Message}\npath={path}");
        }

        notes.Sort((a, b) => a.time.CompareTo(b.time));
        Debug.Log($"[ObjClone] SUS parsed (normal/flick/long) {notes.Count} notes from {Path.GetFileName(path)}");
        return notes;
    }
}