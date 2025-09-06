using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger2 : MonoBehaviour
{
    public void LoadGameScene()
    {
        SceneManager.LoadScene("settings"); // "GameScene"を遷移先のシーン名に
    }
}