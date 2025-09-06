using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger3 : MonoBehaviour
{
    public void LoadGameScene()
    {
        SceneManager.LoadScene("home"); // "GameScene"を遷移先のシーン名に
    }
}