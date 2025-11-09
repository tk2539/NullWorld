using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsController : MonoBehaviour
{
    public Slider noteSpeedSlider;      // ノーツ速度スライダー
    public TMP_Text noteSpeedLabel;     // ラベル "x1.0" 表示用

    void Start()
    {
        // 保存済み値をロード
        float savedSpeed = PlayerPrefs.GetFloat("NoteSpeed", 1.0f);
        noteSpeedSlider.value = savedSpeed;
        UpdateLabel(savedSpeed);

        // スライダーが動いた時に保存する
        noteSpeedSlider.onValueChanged.AddListener(OnNoteSpeedChanged);
    }

    void OnNoteSpeedChanged(float value)
    {
        PlayerPrefs.SetFloat("NoteSpeed", value);  // 値を保存
        PlayerPrefs.Save();                        // ディスクに確定
        UpdateLabel(value);
    }

    void UpdateLabel(float value)
    {
        if (noteSpeedLabel != null)
            noteSpeedLabel.text = $"Note Speed: x{value:0.0}";
    }
}