using UnityEngine;

/// <summary>
/// EscキーまたはExitボタンでアプリを終了するスクリプト
/// </summary>
public class Exit : MonoBehaviour
{
    void Update()
    {
        // Escキーで終了
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            QuitGame();
        }
    }

    // ButtonのOnClick()に割り当てる
    public void QuitGame()
    {
        // 保存など必要があればここで
        PlayerPrefs.Save();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // エディタ実行中は停止
#else
        Application.Quit(); // 実機ビルドで終了
#endif
    }
}