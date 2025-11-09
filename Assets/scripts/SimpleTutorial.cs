using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class SimpleTutorial : MonoBehaviour
{
    [Header("UI")]
    public Image slideImage;
    public TMP_Text continueText;
    public Button skipButton;

    [Header("Slides")]
    public Sprite[] slides;  // スライド画像をインスペクタで設定

    private int index = 0;

    void Start()
    {
        if (slides.Length > 0)
            slideImage.sprite = slides[0];

        if (skipButton)
            skipButton.onClick.AddListener(() => SceneManager.LoadScene("home"));

        if (continueText)
            continueText.text = "Space / Enter で次へ";
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            index++;
            if (index >= slides.Length)
            {
                // チュートリアル終了 → ホームへ
                SceneManager.LoadScene("home");
            }
            else
            {
                slideImage.sprite = slides[index];
            }
        }
    }
}