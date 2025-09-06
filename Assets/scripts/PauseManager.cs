using UnityEngine;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("UI (任意)")]
    public CanvasGroup pauseOverlay; // 背景暗転や「一時停止」テキスト
    public Button resumeButton;
    public Button retryButton;       // 任意：ゲーム側のリトライに接続
    public Button quitButton;        // 任意：選曲へ戻る等

    bool isPaused;

    void Start()
    {
        SetOverlay(false);
        if (resumeButton) resumeButton.onClick.AddListener(TogglePause);
        if (retryButton)  retryButton.onClick.AddListener(OnRetry);
        if (quitButton)   quitButton.onClick.AddListener(OnQuit);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            Time.timeScale = 0f;
            if (MusicPlayer.Instance) MusicPlayer.Instance.Pause();
        }
        else
        {
            Time.timeScale = 1f;
            if (MusicPlayer.Instance) MusicPlayer.Instance.UnPause();
        }
        SetOverlay(isPaused);
    }

    void SetOverlay(bool show)
    {
        if (!pauseOverlay) return;
        pauseOverlay.alpha = show ? 1f : 0f;
        pauseOverlay.interactable = show;
        pauseOverlay.blocksRaycasts = show;
    }

    // 任意：ゲーム固有のリトライ・離脱にフック
    void OnRetry()
    {
        // 例：リザルト経由せず即やり直ししたい場合に使う
        Time.timeScale = 1f;
        if (MusicPlayer.Instance) MusicPlayer.Instance.FadeOut(0.2f); // 好みで
        // ここであなたのゲームのリトライ処理を呼ぶ：
        // SceneManager.LoadScene("game");
    }

    void OnQuit()
    {
        Time.timeScale = 1f;
        if (MusicPlayer.Instance) MusicPlayer.Instance.FadeOut(0.2f);
        // ここで選曲シーン等へ：
        // SceneManager.LoadScene("difficulty");
    }
}