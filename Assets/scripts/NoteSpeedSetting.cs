using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NoteSpeedSetting : MonoBehaviour
{
    [Header("UI Refs")]
    public Slider speedSlider;   // 6.0〜12.0, 0.1刻み想定
    public TextMeshProUGUI speedLabel;      // "Speed: 10.0" のように表示

    void Start()
    {
        // スライダー初期化
        float saved = PlayerPrefs.GetFloat("NoteSpeed", 10.0f);
        if (speedSlider != null)
        {
            speedSlider.minValue = 6.0f;
            speedSlider.maxValue = 12.0f;
            speedSlider.wholeNumbers = false;
            speedSlider.value = Mathf.Clamp(saved, 6.0f, 12.0f);
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);
        }
        UpdateLabel(saved);

        // ObjClone に即時反映（設定画面から直接ゲームへ行くケースに備える）
        ObjClone.userSpeedMultiplier = Mathf.Clamp(saved, 6.0f, 12.0f) / 10.0f;
    }

    void OnSpeedChanged(float value)
    {
        // 0.1刻みに丸め（UnityのSliderにステップは無いので手動で丸める）
        float snapped = Mathf.Round(value * 10f) / 10f;
        if (speedSlider != null && !Mathf.Approximately(speedSlider.value, snapped))
        {
            speedSlider.SetValueWithoutNotify(snapped);
        }

        PlayerPrefs.SetFloat("NoteSpeed", snapped);
        PlayerPrefs.Save();
        UpdateLabel(snapped);

        // ゲームロジックへ即時反映
        ObjClone.userSpeedMultiplier = snapped / 10.0f; // 10.0が基準
    }

    void UpdateLabel(float val)
    {
        if (speedLabel != null) speedLabel.text = $"{val:F1}";
    }
}