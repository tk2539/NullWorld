using UnityEngine;
using UnityEngine.UI;

public class SuddenPlusManager : MonoBehaviour
{
    public Image coverImage; // サドプラの黒い矩形
    [Range(0f, 0.8f)] public float coverHeight = 0.3f;

    private RectTransform rect;

    void Start()
    {
        rect = coverImage.GetComponent<RectTransform>();

        // 保存されている値をロード
        coverHeight = PlayerPrefs.GetFloat("SuddenPlusHeight", 0.3f);
        UpdateCover();
    }

    public void OnSliderChanged(float val)
    {
        coverHeight = Mathf.Clamp(val, 0f, 0.8f);
        PlayerPrefs.SetFloat("SuddenPlusHeight", coverHeight);
        PlayerPrefs.Save();
        UpdateCover();
    }

    void UpdateCover()
    {
        if (rect == null) return;

        // Canvas 全体に対して上部を覆う
        rect.anchorMin = new Vector2(0, 1 - coverHeight);
        rect.anchorMax = new Vector2(1, 1);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        coverImage.color = new Color(0, 0, 0, 1.0f); // 半透明の黒
    }
}