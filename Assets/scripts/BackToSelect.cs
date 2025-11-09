using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ゲーム中にBackspaceキーで選曲画面(select)に戻る
/// </summary>
public class BackToSelect : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            // BGM停止
            if (MusicPlayer.Instance)
                MusicPlayer.Instance.Stop();

            // 選曲画面に戻る
            SceneManager.LoadScene("select");
        }
    }
}