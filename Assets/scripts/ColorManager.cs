using UnityEngine;
using TMPro;
using UnityEngine.SocialPlatforms.Impl;

public class ColortManager : MonoBehaviour
{
    public TMP_Text rankText;

    void Update()
    {       
        int score = PlayerPrefs.GetInt("Score", 0);

        if (score >= 6000)
        {
            rankText.text = "S";
            rankText.color = new Color32(255, 0, 0, 255);         // 赤
        }
        else if (score >= 3000)
        {
            rankText.text = "A";
            rankText.color = new Color32(255, 0, 255, 255);       // 紫
        }
        else if (score >= 2000)
        {
            rankText.text = "B";
            rankText.color = new Color32(0, 0, 255, 255);         // 青
        }
        else if (score >= 1000)
        {
            rankText.text = "C";
            rankText.color = new Color32(0, 255, 255, 255);       // シアン
        }
        else
        {
            rankText.text = "D";
            rankText.color = new Color32(0, 255, 0, 255);         // 緑
        }
    }
}