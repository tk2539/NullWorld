using UnityEngine;

public class TouchLaneInput : MonoBehaviour
{
    // ObjJudgement への参照をアサイン（Inspectorで）
    public ObjJudgement judge;

    void Update()
    {
        // マウスもタッチ扱いで拾う（エディタ確認用）
        if (Input.GetMouseButtonDown(0)) TriggerByScreenPos(Input.mousePosition);

        // 実機タッチ
        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Began) TriggerByScreenPos(t.position);
        }
    }

    void TriggerByScreenPos(Vector2 pos)
    {
        float w = Screen.width;
        int lane = Mathf.Clamp(Mathf.FloorToInt(pos.x / (w / 4f)), 0, 3); // 0..3
        // 既存のグループインデックスに合わせて +1 してるはずなら補正
        int groupIndex = lane + 1;
        bool isFlick = false; // 最小実装ではフリック無効（後述で追加可）

        if (judge != null) judge.TriggerGroup(groupIndex, isFlick);
    }
}