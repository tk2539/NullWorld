using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger1 : MonoBehaviour
{
    public void LoadGameScene()
    {
        SceneManager.LoadScene("select"); // "GameScene"を遷移先のシーン名に
    }
}