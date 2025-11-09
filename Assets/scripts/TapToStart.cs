using UnityEngine;
using UnityEngine.SceneManagement;

public class TapToStart : MonoBehaviour
{
    void Update()
    {
        // マウス/タッチは無視して、キーボードのみで反応させる
        bool mouse = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
        bool touch = Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;

        if (Input.anyKeyDown && !mouse && !touch)
        {
            SceneManager.LoadScene("home");
        }
    }
}