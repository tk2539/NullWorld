// ResultBootstrapFromPrefs.cs （結果シーンに置く）
using UnityEngine;

public class ResultBootstrapFromPrefs : MonoBehaviour
{
    [SerializeField] ResultUI ui;

    void Start()
    {
        if (ui == null) ui = FindObjectOfType<ResultUI>();
        if (ui == null) return;

        int p  = PlayerPrefs.GetInt("PerfectCount", 0);
        int gr = PlayerPrefs.GetInt("GreatCount",   0);
        int go = PlayerPrefs.GetInt("GoodCount",    0);
        int b  = PlayerPrefs.GetInt("BadCount",     0);
        int m  = PlayerPrefs.GetInt("MissCount",    0);
        int mc = PlayerPrefs.GetInt("MaxCombo",     0);
        int sc = PlayerPrefs.GetInt("Score",        0);
        int ms = PlayerPrefs.GetInt("MaxScore",     1000000);

        ui.SetResultValues(p, gr, go, b, m, mc, sc, ms);
        ui.StartReveal(); // ← カウントアップ＆バー＆ランク表示開始
    }
}