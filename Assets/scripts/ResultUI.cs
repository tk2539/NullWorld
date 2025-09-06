using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResultUI : MonoBehaviour
{
    [Header("Result Texts")]
    public TMP_Text perfectText;
    public TMP_Text greatText;
    public TMP_Text goodText;
    public TMP_Text badText;
    public TMP_Text missText;

    public TMP_Text maxComboText;
    public TMP_Text scoreText;
    public TMP_Text accuracyText;     // 任意（%表示したい場合）
    public TMP_Text rankText;         // 最後にドンと表示

    [Header("Score Bar (どちらかを使用)")]
    public Image scoreBarFillImage;   // Image(type=Filled) を使う場合
    public Slider scoreBarSlider;     // Slider を使う場合

    [Header("Score Bar (Rect)")]
    public RectTransform barMask;      // 固定サイズの枠（RectMask2D を付与）
    public RectTransform barFillRect;  // 上の子。虹色Image。左基準で幅を伸ばす

    [Header("Animation")]
    public float numberAnimDuration = 1.0f;
    public float barAnimDuration    = 0.8f;
    public float rankDelayAfterBar  = 0.2f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);

    [Header("Final Values (ResultManagerから流し込む)")]
    public int finalPerfect;
    public int finalGreat;
    public int finalGood;
    public int finalBad;
    public int finalMiss;
    public int finalMaxCombo;
    public int finalScore;
    public int maxPossibleScore = 1000000; // スコア満点（バー用）。あるなら実値に置き換え。

    // 精度に基づくランク（必要なら閾値は調整）
    private readonly (string rank, float minAcc)[] rankTable = new (string, float)[]
    {
        ("SSS", 0.990f),
        ("SS",  0.970f),
        ("S",   0.930f),
        ("A",   0.850f),
        ("B",   0.750f),
        ("C",   0.600f),
        ("D",   0.000f),
    };

    void Start()
    {
        // 参照が未設定でも落ちないように最低限の初期化
        if (scoreBarFillImage) scoreBarFillImage.fillAmount = 0f;
        if (scoreBarSlider)    scoreBarSlider.value = 0f;
        if (rankText)          rankText.alpha = 0f;
        if (barFillRect) SetBarRatio(0f);

        // ここで外部が値をセット済みなら、そのまま演出開始
        StartReveal();
    }

    /// <summary>
    /// 値を外部からまとめて流し込みたい場合
    /// </summary>
    public void SetResultValues(int perfect, int great, int good, int bad, int miss,
                                int maxCombo, int score, int maxScore)
    {
        finalPerfect = perfect;
        finalGreat   = great;
        finalGood    = good;
        finalBad     = bad;
        finalMiss    = miss;
        finalMaxCombo= maxCombo;
        finalScore   = score;
        if (maxScore > 0) maxPossibleScore = maxScore;
    }

    public void StartReveal()
    {
        StopAllCoroutines();
        StartCoroutine(RevealRoutine());
    }

    private IEnumerator RevealRoutine()
    {
        // 1) 数字カウントアップ
        yield return StartCoroutine(CountUpText(perfectText, finalPerfect));
        yield return StartCoroutine(CountUpText(greatText,   finalGreat));
        yield return StartCoroutine(CountUpText(goodText,    finalGood));
        yield return StartCoroutine(CountUpText(badText,     finalBad));
        yield return StartCoroutine(CountUpText(missText,    finalMiss));
        yield return StartCoroutine(CountUpText(maxComboText,finalMaxCombo));
        yield return StartCoroutine(CountUpText(scoreText,   finalScore));

        // 2) スコアバー伸張
        float ratio = Mathf.Clamp01(maxPossibleScore > 0 ? (float)finalScore / maxPossibleScore : 0f);
        yield return StartCoroutine(AnimateBar(ratio));

        // 3) ランク表示
        yield return new WaitForSeconds(rankDelayAfterBar);
        ShowRank(CalcAccuracy());
    }

    private IEnumerator CountUpText(TMP_Text t, int target)
    {
        if (t == null) yield break;

        int from = 0;
        float t0 = 0f;
        while (t0 < numberAnimDuration)
        {
            t0 += Time.unscaledDeltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t0 / numberAnimDuration));
            int v = Mathf.RoundToInt(Mathf.Lerp(from, target, k));
            t.text = v.ToString();
            yield return null;
        }
        t.text = target.ToString();
    }

    private void SetBarRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        // Rect ベースのバー
        if (barFillRect && barMask)
        {
            float w = ((RectTransform)barMask).rect.width * ratio;
            var sd = barFillRect.sizeDelta;
            // 幅のみ変更（高さはレイアウトに任せる）
            sd.x = w;
            barFillRect.sizeDelta = sd;
        }
        // 既存の Image/Slider も同時にサポート
        if (scoreBarFillImage) scoreBarFillImage.fillAmount = ratio;
        if (scoreBarSlider)    scoreBarSlider.value       = ratio;
    }

    private IEnumerator AnimateBar(float targetRatio)
    {
        // RectTransform バーか、従来の Image/Slider のいずれかがあれば実行
        if (barFillRect == null && barMask == null && scoreBarFillImage == null && scoreBarSlider == null)
            yield break;

        float from = 0f;
        float t0 = 0f;
        while (t0 < barAnimDuration)
        {
            t0 += Time.unscaledDeltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t0 / barAnimDuration));
            float v = Mathf.Lerp(from, targetRatio, k);
            SetBarRatio(v);
            yield return null;
        }
        SetBarRatio(targetRatio);
    }

    private float CalcAccuracy()
    {
        // ざっくり精度：Perfect=1.0, Great=0.8, Good=0.5, Bad/Miss=0
        int total = finalPerfect + finalGreat + finalGood + finalBad + finalMiss;
        if (total <= 0) return 0f;
        float score =
            finalPerfect * 1.0f +
            finalGreat   * 0.8f +
            finalGood    * 0.5f;
        float acc = score / total;
        if (accuracyText) accuracyText.text = $"{Mathf.RoundToInt(acc * 100f)}%";
        return acc;
    }

    private void ShowRank(float acc)
    {
        if (rankText == null) return;

        string rank = "D";
        foreach (var r in rankTable)
        {
            if (acc >= r.minAcc) { rank = r.rank; break; }
        }
        rankText.text = rank;
        StartCoroutine(FadeInRank());
    }

    private IEnumerator FadeInRank()
    {
        // ふわっと表示（拡大&フェード）
        float dur = 0.35f;
        float t0 = 0f;
        var rt = rankText.rectTransform;
        Vector3 from = Vector3.one * 1.4f;
        Vector3 to   = Vector3.one;

        rankText.alpha = 0f;
        rt.localScale = from;

        while (t0 < dur)
        {
            t0 += Time.unscaledDeltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t0 / dur));
            rankText.alpha = k;
            rt.localScale  = Vector3.Lerp(from, to, k);
            yield return null;
        }
        rankText.alpha = 1f;
        rt.localScale = to;
    }
}