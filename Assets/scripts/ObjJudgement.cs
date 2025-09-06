using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class ObjJudgement : MonoBehaviour
{
    [Header("Judge")]
    public float judgeWindow = 0.05f; // 100ms
    public GameObject noteParent;

    [Header("HUD")]
    public TMPro.TMP_Text comboText;
    public TMPro.TMP_Text lifeText;
    public TMPro.TMP_Text scoreText;
    public TMPro.TMP_Text rankText;
    public Image lifeBar;

    [Header("SE")]
    private AudioSource audioSource;
    public AudioClip normalSE, criticalSE, flickSE, longSE, missSE;

    [Header("State")]
    public int life = 1000;
    public int maxScore = 0;
    public int score = 0;
    public int combo = 0, maxComboCount = 0;
    public int perfectCount = 0, greatCount = 0, goodCount = 0, badCount = 0, missCount = 0;

    // ===== Queue-based judgement (軽量化) =====
    // 4グループ(1:1-3, 2:4-6, 3:7-9, 4:10-12) × 4種別(normal/critical/flick/longstart)
    // 先頭のみを見ることで総当たり検索を回避
    private readonly Queue<NoteInfo>[][] lanes = new Queue<NoteInfo>[4][];
    private bool queuesBuilt = false;

    // 動的生成ノーツ取り込み用
    private HashSet<NoteInfo> _enqueued = new HashSet<NoteInfo>();
    private HashSet<NoteInfo> _consumed = new HashSet<NoteInfo>();
    private int _lastChildCount = -1;
    private float _lastScanTime = -1f;
    private const float SCAN_INTERVAL = 0.6f; // 600ms ごとに軽く走査

    // 種別インデックス
    int TypeIndex(string s)
    {
        if (s == "normal") return 0;
        if (s == "critical") return 1;
        if (s == "flick") return 2;
        if (s == "longstart") return 3;
        return -1;
    }
    // レーン→グループ(0..3)
    int LaneToGroupIndex(int lane)
    {
        if (lane <= 3) return 0;  // 1..3  → D/E
        if (lane <= 6) return 1;  // 4..6  → F/R
        if (lane <= 9) return 2;  // 7..9  → J/U
        if (lane <= 12) return 3; // 10..12→ K/I
        return -1;
    }

    void BuildQueues()
    {
        for (int i = 0; i < 4; i++) { lanes[i] = new Queue<NoteInfo>[4]; for (int t = 0; t < 4; t++) lanes[i][t] = new Queue<NoteInfo>(); }
        if (!noteParent) { queuesBuilt = true; return; }
        var all = noteParent.GetComponentsInChildren<NoteInfo>(true);
        // 到達時刻で昇順にしてから各キューへ
        System.Array.Sort(all, (a, b) => a.arrivalTime.CompareTo(b.arrivalTime));
        foreach (var n in all)
        {
            if (n == null || n.fake) continue;
            if (n.type == "longroot") continue; // 棒は判定対象外
            int ti = TypeIndex(n.type);
            if (ti < 0) continue;
            var nr = NoteToRange(n);
            // 幅に応じてまたがる全グループへキュー投入
            for (int gi = 1; gi <= 4; gi++)
            {
                var gr = GroupToRange(gi);
                if (Overlaps(nr, gr))
                {
                    lanes[gi - 1][ti].Enqueue(n);
                }
            }
            _enqueued.Add(n);
        }
        _lastChildCount = noteParent.transform.childCount;
        _lastScanTime = Time.time;
        queuesBuilt = true;
    }

    // 途中で生成された NoteInfo を差分取り込み
    void EnqueueNewNotes()
    {
        if (!noteParent) return;
        // 子の数が増えた / 一定時間経過 なら軽く再スキャン
        if (noteParent.transform.childCount == _lastChildCount && (Time.time - _lastScanTime) < SCAN_INTERVAL)
            return;

        var all = noteParent.GetComponentsInChildren<NoteInfo>(true);
        // 追加分だけ enqueue（全件総当たりせず HashSet で差分）
        foreach (var n in all)
        {
            if (n == null || n.fake) continue;
            if (n.type == "longroot") continue;
            if (_enqueued.Contains(n)) continue;

            int ti = TypeIndex(n.type);
            if (ti < 0) { _enqueued.Add(n); continue; }
            var nr = NoteToRange(n);
            for (int gi = 1; gi <= 4; gi++)
            {
                var gr = GroupToRange(gi);
                if (Overlaps(nr, gr))
                {
                    lanes[gi - 1][ti].Enqueue(n);
                }
            }
            _enqueued.Add(n);
        }
        _lastChildCount = noteParent.transform.childCount;
        _lastScanTime = Time.time;
    }

    // ===== 生成側(ObjCloneなど)からの明示登録API =====
    public void RegisterNote(NoteInfo n)
    {
        if (n == null) return;
        if (n.fake) { _enqueued.Add(n); return; }
        if (n.type == "longroot") { _enqueued.Add(n); return; } // 棒は判定不要
        int ti = TypeIndex(n.type);
        if (ti < 0) { _enqueued.Add(n); return; }
        if (_enqueued.Contains(n)) return; // 多重登録防止
        if (!queuesBuilt) BuildQueues();
        var nr = NoteToRange(n);
        for (int gi = 1; gi <= 4; gi++)
        {
            var gr = GroupToRange(gi);
            if (Overlaps(nr, gr))
            {
                lanes[gi - 1][ti].Enqueue(n);
            }
        }
        _enqueued.Add(n);
        _lastChildCount = noteParent ? noteParent.transform.childCount : _lastChildCount;
        _lastScanTime = Time.time;
    }

    public void RegisterSpawn(Transform root)
    {
        if (!root) return;
        // 単体でも親でもOK。配下の NoteInfo をまとめて登録
        var infos = root.GetComponentsInChildren<NoteInfo>(true);
        foreach (var n in infos) RegisterNote(n);
    }

    // キュー先頭だけで判定
    void JudgeFromQueue(int groupIndex, int typeIndex)
    {
        var q = lanes[groupIndex][typeIndex];
        while (q.Count > 0)
        {
            var n = q.Peek();
            if (_consumed.Contains(n)) { q.Dequeue(); continue; }
            if (n == null) { q.Dequeue(); continue; }
            float now = Time.time - n.startTime;
            float diff = Mathf.Abs(n.arrivalTime - now);

            if (diff <= judgeWindow)            { ApplyHit(n, n.transform, Judge.Perfect); q.Dequeue(); return; }
            else if (diff <= judgeWindow * 2f)  { ApplyHit(n, n.transform, Judge.Great);   q.Dequeue(); return; }
            else if (diff <= judgeWindow * 3f)  { ApplyHit(n, n.transform, Judge.Good);    q.Dequeue(); return; }
            else if (n.arrivalTime + judgeWindow * 5f < now)
            {
                // AutoMiss 相当（取りこぼし防止）
                ApplyMiss(n, n.transform); q.Dequeue(); continue;
            }
            // まだ手前 → 次フレームまで待つ
            break;
        }
    }

    // 全レーンの先頭だけを自動ミス処理
    void AutoMissQueues()
    {
        for (int g = 0; g < 4; g++)
        {
            for (int t = 0; t < 4; t++)
            {
                var q = lanes[g][t];
                while (q.Count > 0)
                {
                    var n = q.Peek();
                    if (_consumed.Contains(n)) { q.Dequeue(); continue; }
                    if (n == null) { q.Dequeue(); continue; }
                    if (n.type == "longroot") { q.Dequeue(); continue; }
                    float now = Time.time - n.startTime;
                    if (now > n.arrivalTime + judgeWindow * 5f)
                    {
                        if (n.fake) { if (n) Destroy(n.gameObject); q.Dequeue(); continue; }
                        ApplyMiss(n, n.transform);
                        q.Dequeue();
                        continue;
                    }
                    // 先頭がまだミス圏外 → このグループは終了
                    break;
                }
            }
        }
    }

    const int MaxLife = 1000;
    float lifeBarMaxWidth = 150f;

    void Start()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        CalcMaxScore();
        UpdateHUDAll();
        BuildQueues();
        EnqueueNewNotes();
        Debug.Log("[ObjJudgement] Ready. You can call RegisterSpawn/RegisterNote from spawners.");
    }

    void Update()
    {
        if (!queuesBuilt) BuildQueues();

        // 途中生成ノーツの取り込み（低頻度差分スキャン）
        EnqueueNewNotes();

        // ===== 非フリック: d/f/j/k =====
        if (Keyboard.current.dKey.wasPressedThisFrame) TriggerGroup(1, isFlick:false);
        if (Keyboard.current.fKey.wasPressedThisFrame) TriggerGroup(2, isFlick:false);
        if (Keyboard.current.jKey.wasPressedThisFrame) TriggerGroup(3, isFlick:false);
        if (Keyboard.current.kKey.wasPressedThisFrame) TriggerGroup(4, isFlick:false);

        // ===== フリック: e/r/u/i =====
        if (Keyboard.current.eKey.wasPressedThisFrame) TriggerGroup(1, isFlick:true);
        if (Keyboard.current.rKey.wasPressedThisFrame) TriggerGroup(2, isFlick:true);
        if (Keyboard.current.uKey.wasPressedThisFrame) TriggerGroup(3, isFlick:true);
        if (Keyboard.current.iKey.wasPressedThisFrame) TriggerGroup(4, isFlick:true);

        AutoMiss();
        UpdateHUDAll();
        UpdateRankDisplay();
        GameEnd();
    }

    // ── 1..4 のグループ（1:1-3, 2:4-6, 3:7-9, 4:10-12）
    public void TriggerGroup(int groupIndex, bool isFlick)
    {
        if (!queuesBuilt) return;
        int gi = Mathf.Clamp(groupIndex - 1, 0, 3);
        if (isFlick)
        {
            // flick のみ
            JudgeFromQueue(gi, 2);
        }
        else
        {
            // normal / critical / longstart の順に1個だけ判定
            JudgeFromQueue(gi, 0);
            JudgeFromQueue(gi, 1);
            JudgeFromQueue(gi, 3);
        }
    }

    // ──幅付きノーツの範囲
    struct LaneRange { public int from, to; public LaneRange(int f,int t){from=f;to=t;} }
    LaneRange NoteToRange(NoteInfo n)
    {
        int w = 1;
        try { w = Mathf.Max(1, n.width); } catch { w = 1; }

        int start = Mathf.Clamp(n.lane, 1, 12);
        int end = Mathf.Clamp(n.lane + w - 1, 1, 12);
        return new LaneRange(start, end);
    }
    LaneRange GroupToRange(int gi)
    {
        switch (gi)
        {
            case 1: return new LaneRange(1, 3);
            case 2: return new LaneRange(4, 6);
            case 3: return new LaneRange(7, 9);
            default: return new LaneRange(10, 12);
        }
    }
    bool Overlaps(LaneRange a, LaneRange b) => (a.from <= b.to) && (b.from <= a.to);

    // ── 判定適用
    enum Judge { Perfect, Great, Good }

    // ===== Score & Life helpers =====
    int GetBaseScore(NoteInfo n)
    {
        if (n == null) return 0;
        if (n.type == "critical")   return 200;
        if (n.type == "longstart")  return 300;  // startでまとめて加点
        if (n.type == "longroot")   return 0;    // 棒は加点なし
        // normal / flick
        return 100;
    }

    float GetJudgeMultiplier(Judge j)
    {
        switch (j)
        {
            case Judge.Perfect: return 1.0f;
            case Judge.Great:   return 0.7f;
            case Judge.Good:    return 0.5f;
        }
        return 0f;
    }

    int GetMissDamage(NoteInfo n)
    {
        // miss時: normal/flick/critical = -80, long = -200
        if (n != null && n.type == "longstart") return 200;
        return 80;
    }

    int GetBadDamage(NoteInfo n)
    {
        // bad時は miss の 5/8 倍（0.625）
        int miss = GetMissDamage(n);
        return Mathf.RoundToInt(miss * 0.625f); // 80→50, 200→125
    }

    void ApplyHit(NoteInfo note, Transform tf, Judge j)
    {
        if (note) _consumed.Add(note);
        // ノーツごとSE
        PlayNoteSE(note.type);

        // スコア（Perfect:1.0, Great:0.7, Good:0.5）
        int baseScore = GetBaseScore(note);
        float mul = GetJudgeMultiplier(j);
        score += Mathf.RoundToInt(baseScore * mul);

        // カウント
        if (j == Judge.Perfect)      perfectCount++;
        else if (j == Judge.Great)   greatCount++;
        else                         goodCount++;

        // 破棄（longstartは親ごと）
        if (note.type == "longstart" && tf && tf.parent) Destroy(tf.parent.gameObject);
        else if (tf) Destroy(tf.gameObject);

        // コンボ：通常は+1、longstartは合計 3/2/1（Perfect/Great/Good）
        int addCombo = 1;
        if (note.type == "longstart")
        {
            addCombo = (j == Judge.Perfect) ? 3 : (j == Judge.Great) ? 2 : 1;
        }
        combo += addCombo;
        if (combo > maxComboCount) maxComboCount = combo;
    }

    void ApplyBad(NoteInfo note, Transform tf)
    {
        if (note) _consumed.Add(note);
        if (audioSource && missSE) audioSource.PlayOneShot(missSE);
        if (note.type == "longstart" && tf && tf.parent) Destroy(tf.parent.gameObject);
        else if (tf) Destroy(tf.gameObject);
        combo = 0; badCount++;
        life -= GetBadDamage(note);
    }

    void ApplyMiss(NoteInfo note, Transform tf)
    {
        if (note) _consumed.Add(note);
        if (audioSource && missSE) audioSource.PlayOneShot(missSE);
        if (note.type == "longstart" && tf && tf.parent) Destroy(tf.parent.gameObject);
        else if (tf) Destroy(tf.gameObject);
        combo = 0; missCount++;
        life -= GetMissDamage(note);
    }

    void PlayNoteSE(string type)
    {
        if (!audioSource) return;
        if      (type == "critical" && criticalSE) audioSource.PlayOneShot(criticalSE);
        else if (type == "longstart" && longSE)    audioSource.PlayOneShot(longSE);
        else if (type == "flick" && flickSE)       audioSource.PlayOneShot(flickSE);
        else if (normalSE)                         audioSource.PlayOneShot(normalSE);
    }

    void AutoMiss()
    {
        if (!queuesBuilt) return;
        AutoMissQueues();
    }

    // ── HUD/終了
    public void CalcMaxScore()
    {
        maxScore = 0;
        if (!noteParent) return;
        var all = noteParent.GetComponentsInChildren<NoteInfo>(true);
        foreach (var n in all)
        {
            if (n == null || n.fake) continue;
            maxScore += GetBaseScore(n);
        }
        PlayerPrefs.SetInt("MaxScore", maxScore);
    }

    void AddScore(NoteInfo n, int baseScore)
    {
        // deprecated (kept for back-compat): not used anymore
        score += baseScore;
    }

    // removed int CalculateScore(NoteInfo n, int baseScore)

    void GiveLife(int v) { life = Mathf.Min(MaxLife, life + v); }
    void UpdateHUDAll()
    {
        if (comboText) comboText.text = combo.ToString();
        if (lifeText)  lifeText.text  = Mathf.Max(0, life).ToString();
        if (scoreText) scoreText.text = score.ToString();
        UpdateLifeBar();
    }
    void UpdateLifeBar()
    {
        if (!lifeBar) return;
        if (lifeBar.type == Image.Type.Filled)
        {
            lifeBar.fillAmount = Mathf.Clamp01(life / (float)MaxLife);
        }
        else
        {
            var rt = lifeBar.rectTransform;
            float ratio = Mathf.Clamp01(life / (float)MaxLife);
            rt.sizeDelta = new Vector2(lifeBarMaxWidth * ratio, rt.sizeDelta.y);
            lifeBar.color = (life >= 200) ? Color.green : Color.red;
        }
    }
    void UpdateRankDisplay()
    {
        if (!rankText) return;
        int refMax = (maxScore > 0) ? maxScore : PlayerPrefs.GetInt("MaxScore", 1);
        float rate = refMax <= 0 ? 0f : (float)score / refMax;

        if      (rate > 0.8f){ rankText.text = "S"; rankText.color = new Color32(255,0,0,255);}
        else if (rate > 0.6f){ rankText.text = "A"; rankText.color = new Color32(255,0,255,255);}
        else if (rate > 0.4f){ rankText.text = "B"; rankText.color = new Color32(0,0,255,255);}
        else if (rate > 0.2f){ rankText.text = "C"; rankText.color = new Color32(0,255,255,255);}
        else                  { rankText.text = "D"; rankText.color = new Color32(0,255,0,255);}
    }

    public void GameEnd()
    {
        if (!noteParent) return;
        var remain = noteParent.GetComponentsInChildren<NoteInfo>(false);
        if (remain.Length == 0) StartCoroutine(GameEndRoutine());
    }
    IEnumerator GameEndRoutine()
    {
        PlayerPrefs.SetInt("PerfectCount", perfectCount);
        PlayerPrefs.SetInt("GreatCount",   greatCount);
        PlayerPrefs.SetInt("GoodCount",    goodCount);
        PlayerPrefs.SetInt("BadCount",     badCount);
        PlayerPrefs.SetInt("MissCount",    missCount);
        PlayerPrefs.SetInt("MaxCombo",     maxComboCount);
        PlayerPrefs.SetInt("Score",        score);
        PlayerPrefs.SetInt("MaxScore",     maxScore);
        PlayerPrefs.Save();

        yield return new WaitForSeconds(1.0f);
        SceneManager.LoadScene("result");
    }
}