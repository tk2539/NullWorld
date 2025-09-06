using UnityEngine;
using TMPro;

public class BlinkText : MonoBehaviour
{
    public TMP_Text tapToStartText; // インスペクターでTextをアタッチ
    public float blinkSpeed = 1.5f; // 点滅の速さ

    void Update()
    {
        float alpha = Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed)); // 0~1を周期で往復
        Color c = tapToStartText.color;
        c.a = alpha;
        tapToStartText.color = c;
    }
}