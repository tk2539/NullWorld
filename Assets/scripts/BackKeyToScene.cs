using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Backspaceキーで指定シーンに戻る
/// </summary>
public class BackKeyToScene : MonoBehaviour
{
    [Tooltip("戻り先のシーン名")]
    public string targetScene = "home";

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            // BGM停止
            if (MusicPlayer.Instance)
                MusicPlayer.Instance.Stop();

            // 指定したシーンに移動
            SceneManager.LoadScene(targetScene);
        }
    }
}