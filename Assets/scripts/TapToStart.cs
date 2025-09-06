using UnityEngine;
using UnityEngine.SceneManagement;

public class TapToStart : MonoBehaviour
{
    void Update()
    {
        // マウスクリックまたは画面タップ
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            // シーン切り替え
            SceneManager.LoadScene("home"); // "GameScene"を次のシーン名に
        }

        // キーボード（スペースやエンターでも進むなら）
        if (Input.anyKeyDown)
        {
            SceneManager.LoadScene("home");
        }
    }
}