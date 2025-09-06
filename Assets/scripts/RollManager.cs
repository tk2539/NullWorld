using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;

public class RollManager : MonoBehaviour
{
    [Header("キャラ画像のスプライト（好きなだけセット）")]
    public Sprite[] characterSprites;

    [Header("出現確率（%合計100。画像数と同じだけセット）")]
    public float[] probabilities;

    [Header("出たキャラ画像を表示するUI")]
    public Image resultImage;
    [Header("名前のテキスト")]
    public TextMeshPro resultNameText;
    public void RollGacha()
    {
        // 確率判定
        float rand = Random.Range(0f, 100f);
        float sum = 0f;
        int resultIndex = 0;

        for (int i = 0; i < probabilities.Length; i++)
        {
            sum += probabilities[i];
            if (rand < sum)
            {
                resultIndex = i;
                break;
            }
        }

        // 画像表示
        resultImage.sprite = characterSprites[resultIndex];
        resultImage.gameObject.SetActive(true);
    }
}