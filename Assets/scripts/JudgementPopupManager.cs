using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum JudgementType { Perfect, Great, Good, Bad, Miss }

public class JudgementPopupManager : MonoBehaviour
{
    public static JudgementPopupManager I { get; private set; }

    [Header("Refs")]
    public RectTransform canvasRoot;      // Canvas の RectTransform
    public TMP_Text template;             // JudgementTemplate (Disabled)
    public Camera worldCamera;            // ゲーム用カメラ (notesを映してるカメラ)

    [Header("Pool")]
    public int poolSize = 32;

    readonly Queue<TMP_Text> pool = new Queue<TMP_Text>();

    void Awake()
    {
        I = this;
        template.gameObject.SetActive(false);
        for (int i = 0; i < poolSize; i++)
            pool.Enqueue(CreateOne());
        if (worldCamera == null) worldCamera = Camera.main;
    }

    TMP_Text CreateOne()
    {
        var go = Instantiate(template.gameObject, canvasRoot);
        go.SetActive(false);
        return go.GetComponent<TMP_Text>();
    }

    TMP_Text Rent()
    {
        return pool.Count > 0 ? pool.Dequeue() : CreateOne();
    }

    void Return(TMP_Text t)
    {
        t.gameObject.SetActive(false);
        pool.Enqueue(t);
    }

    public void Show(Vector3 worldPos, JudgementType type)
    {
        // 1) ワールド→スクリーン→Canvas座標
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(worldCamera, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRoot, screen, null, out var local);

        // 2) テキスト設定
        var txt = Rent();
        var rt = (RectTransform)txt.transform;
        rt.anchoredPosition = local;
        txt.text = Label(type);
        txt.color = ColorOf(type);
        txt.alpha = 1f;
        txt.transform.localScale = Vector3.one * 0.9f;
        txt.gameObject.SetActive(true);

        // 3) ふわっと上に上がってフェードアウト
        StartCoroutine(PopupAnim(txt));
    }

    IEnumerator PopupAnim(TMP_Text txt)
    {
        float dur = 0.45f;
        float t = 0f;
        var rt = (RectTransform)txt.transform;
        Vector2 start = rt.anchoredPosition;
        Vector2 end   = start + new Vector2(0f, 60f);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime; // ポーズ中でも進めたいなら unscaled
            float u = Mathf.Clamp01(t / dur);
            rt.anchoredPosition = Vector2.Lerp(start, end, u);
            txt.alpha = 1f - u;
            float s = Mathf.Lerp(0.9f, 1.2f, u);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        Return(txt);
    }

    string Label(JudgementType j) =>
        j == JudgementType.Perfect ? "Perfect" :
        j == JudgementType.Great   ? "Great"   :
        j == JudgementType.Good    ? "Good"    :
        j == JudgementType.Bad     ? "Bad"     : "Miss";

    Color ColorOf(JudgementType j) =>
        j == JudgementType.Perfect ? new Color(1f, 0.95f, 0.2f) :  // yellow-ish
        j == JudgementType.Great   ? new Color(0.2f, 0.9f, 1f)  :  // cyan
        j == JudgementType.Good    ? new Color(0.6f, 1f, 0.6f)  :  // green
        j == JudgementType.Bad     ? new Color(0.7f, 0.5f, 1f)  :  // purple
                                     new Color(1f, 0.3f, 0.3f);    // red
}