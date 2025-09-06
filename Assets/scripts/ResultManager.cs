using UnityEngine;
using TMPro;

public class ResultManager : MonoBehaviour
{
    public TMP_Text perfectText, greatText, goodText, badText, missText, maxcomboText, scoreText;

    void Start()
    {
        Debug.Log("ResultManager Start");
        perfectText.text = PlayerPrefs.GetInt("PerfectCount", 0).ToString();
        greatText.text = PlayerPrefs.GetInt("GreatCount", 0).ToString();
        goodText.text = PlayerPrefs.GetInt("GoodCount", 0).ToString();
        badText.text = PlayerPrefs.GetInt("BadCount", 0).ToString();
        missText.text = PlayerPrefs.GetInt("MissCount", 0).ToString();
        maxcomboText.text = PlayerPrefs.GetInt("MaxCombo", 0).ToString();
        scoreText.text = PlayerPrefs.GetInt("Score", 0).ToString();
    }
}