using UnityEngine;
using TMPro;

public class AssetsManager: MonoBehaviour
{
    public TMP_Text coinText; // コイン枚数を表示するText
    int coin = 0;

    void Start()
    {
        // 各判定数はPlayerPrefsで保存されている前提
        int perfect = PlayerPrefs.GetInt("PerfectCount", 0);
        int great   = PlayerPrefs.GetInt("GreatCount", 0);
        int good    = PlayerPrefs.GetInt("GoodCount", 0);

        coin = perfect * 3 + great * 2 + good;
        coinText.text = $"x {coin}";

        // コインをプレイヤーの総資産に加算したい場合
        int totalCoin = PlayerPrefs.GetInt("TotalCoin", 0);
        totalCoin += coin;
        PlayerPrefs.SetInt("TotalCoin", totalCoin);
        PlayerPrefs.Save();
    }
}
