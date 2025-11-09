using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class SelectSceneBootstrap : MonoBehaviour
{
    [Header("必須: ScrollView/Viewport/Content を割り当て")]
    public RectTransform gridContent;

    [Header("右ペイン UI")]
    public SongDetailUI detailUI; // ← Inspector で SongDetailUI をアタッチ

    [Header("カードの表示設定")]
    [Range(0.5f, 1.2f)]
    public float cellScale = 0.85f;
    public Vector2 cellSize = new Vector2(260, 260);
    public Vector2 spacing = new Vector2(20, 20);
    public Texture2D fallbackTexture;

    [Header("レイアウト")]
    [Tooltip("横の固定列数（プロセカ風=3）")]
    public int fixedColumns = 3;

    [Tooltip("1カードの最小幅（Viewport が狭い時は列数を減らしてでもこの幅を優先）")]
    public float minCell = 240f;

    void Start()
    {
        if (!gridContent)
        {
            Debug.LogError("[SelectSceneBootstrap] gridContent 未設定");
            return;
        }
        PrepareGrid(gridContent);
        StartCoroutine(InitAfterLayout());
    }

    System.Collections.IEnumerator InitAfterLayout()
    {
        yield return null;
        if ((gridContent.parent as RectTransform)?.rect.width <= 1f)
            yield return null;
        PopulateCovers();
    }

    void PrepareGrid(RectTransform content)
    {
        var grid = content.GetComponent<GridLayoutGroup>();
        if (!grid) grid = content.gameObject.AddComponent<GridLayoutGroup>();

        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = fixedColumns;

        var fitter = content.GetComponent<ContentSizeFitter>();
        if (!fitter) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(0, 1);
        content.pivot = new Vector2(0, 1);
        content.anchoredPosition = Vector2.zero;
        content.localScale = Vector3.one;
        content.localRotation = Quaternion.identity;
        content.anchoredPosition3D = Vector3.zero;
    }

    void FitCellSizeToViewport(GridLayoutGroup grid)
    {
        var viewport = gridContent.parent as RectTransform;
        if (!viewport)
        {
            grid.cellSize = cellSize;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, fixedColumns);
            return;
        }

        float vw = viewport.rect.width;
        if (vw <= 1f)
        {
            grid.cellSize = cellSize;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, fixedColumns);
            return;
        }

        int cols = Mathf.Max(1, fixedColumns);
        var pad = grid.padding;
        float totalPadding = pad.left + pad.right;
        float totalSpacing = spacing.x * Mathf.Max(0, cols - 1);
        float cellW = Mathf.Floor((vw - totalPadding - totalSpacing) / cols);

        cellW = cellW * Mathf.Clamp(cellScale, 0.5f, 1.2f);
        cellW = Mathf.Max(64f, cellW);

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;
        grid.cellSize = new Vector2(cellW, cellW);
    }

    void PopulateCovers()
    {
        // 既存の子をクリア
        for (int i = gridContent.childCount - 1; i >= 0; --i)
            Destroy(gridContent.GetChild(i).gameObject);

        var grid = gridContent.GetComponent<GridLayoutGroup>();
        if (grid) FitCellSizeToViewport(grid);

        // 両方（persistent優先 + streaming）から曲フォルダを列挙
        var dirs = ChartPaths.EnumerateAllSongDirs();
        if (dirs == null || dirs.Count == 0)
        {
            Debug.Log("[SelectSceneBootstrap] no song directories found.");
            return;
        }

        int created = 0;
        foreach (var dir in dirs)
        {
            string folderName = Path.GetFileName(dir);

            // カバー画像パスを解決（なければ null → fallbackTexture が使われる）
            string coverPath = ChartPaths.ResolveCoverPath(folderName);

            var tex = CoverCache.Get(coverPath, fallbackTexture);
            if (!tex) continue;

            var go = new GameObject(folderName, typeof(RectTransform), typeof(RawImage), typeof(Button));
            go.transform.SetParent(gridContent, false);

            var img = go.GetComponent<RawImage>();
            img.texture = tex;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = cellSize;
            rt.localScale = Vector3.one;

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                Debug.Log($"[SelectSceneBootstrap] Click: {folderName}");
                if (detailUI != null)
                {
                    // 右ペインに渡す（folderName をキーに使う）
                    detailUI.SetData(tex, folderName);
                }
            });
            created++;
        }

        Debug.Log($"[SelectSceneBootstrap] covers populated (children) = {created}");
    }
}