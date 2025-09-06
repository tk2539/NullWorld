using UnityEngine;

[ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RoundedBox : MonoBehaviour
{
    [Header("Size (world units)")]
    public float sizeX = 1f;
    public float sizeY = 0.2f;
    public float sizeZ = 0.2f;

    [Header("Scale Multiplier")]
    [SerializeField] float scaleZMultiplier = 1.0f;

    [Header("Corner")]
    [Range(0f, 0.5f)]
    public float radius = 0.15f;   // 角の丸み（短辺の50%まで）
    [Range(1, 6)]
    public int segments = 3;       // 丸みの細かさ（1〜6）

    [Header("Collider")]
    public bool addMeshCollider = false;

    Mesh _mesh;

    void OnEnable() => Rebuild();
    void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // OnValidate中は直接メッシュ差し替えNG → 次フレームで実行
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this) Rebuild();
            };
        }
        else
        {
            Rebuild();
        }
#else
    Rebuild();
#endif
    }

    void Rebuild()
    {
        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = "RoundedBoxMesh";
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
        else _mesh.Clear();

        // 安全クリップ
        float sx = Mathf.Max(1e-4f, sizeX);
        float sy = Mathf.Max(1e-4f, sizeY);
        float sz = Mathf.Max(1e-4f, sizeZ * scaleZMultiplier);
        float rMax = Mathf.Min(sx, Mathf.Min(sy, sz)) * 0.5f;
        float r = Mathf.Clamp(radius, 0f, rMax);
        int seg = Mathf.Max(1, segments);

        // グリッド分割数
        int nx = seg + 1;
        int ny = seg + 1;
        int nz = seg + 1;

        // 頂点生成（ベースは直方体の内側コアに、角は膨らませる）
        int vx = nx + 1;
        int vy = ny + 1;
        int vz = nz + 1;
        Vector3[] verts = new Vector3[vx * vy * vz];
        Vector3 half = new Vector3(sx, sy, sz) * 0.5f;
        Vector3 inner = new Vector3(half.x - r, half.y - r, half.z - r);

        int idx = 0;
        for (int z = 0; z < vz; z++)
        {
            float tz = (float)z / nz;
            float zPos = Mathf.Lerp(-inner.z, inner.z, tz);
            for (int y = 0; y < vy; y++)
            {
                float ty = (float)y / ny;
                float yPos = Mathf.Lerp(-inner.y, inner.y, ty);
                for (int x = 0; x < vx; x++)
                {
                    float tx = (float)x / nx;
                    float xPos = Mathf.Lerp(-inner.x, inner.x, tx);

                    // 直方体の内側コア
                    Vector3 p = new Vector3(xPos, yPos, zPos);

                    // 角/縁に近い場合は球面方向へ半径rぶん膨らませる
                    Vector3 n = new Vector3(
                        Mathf.Clamp(p.x, -inner.x, inner.x),
                        Mathf.Clamp(p.y, -inner.y, inner.y),
                        Mathf.Clamp(p.z, -inner.z, inner.z)
                    );
                    Vector3 delta = p - n;
                    if (delta.sqrMagnitude > 1e-10f)
                    {
                        delta = delta.normalized * r;
                        p = n + delta;
                    }

                    verts[idx++] = p;
                }
            }
        }

        // 面の三角形
        // 各セル（nx*ny*nz）に対し6面作るが、ここでは外殻だけ張る
        // グリッドから6枚の面(XY, XZ, YZ)を張る方法で三角形化
        // 補助関数で一面ずつ貼る
        System.Collections.Generic.List<int> tris = new System.Collections.Generic.List<int>(vx * vy * 6);
        AddSurface(tris, verts, vx, vy, vz, Axis.Z, vz - 1, true);  // 前面 +Z
        AddSurface(tris, verts, vx, vy, vz, Axis.Z, 0, false); // 背面 -Z
        AddSurface(tris, verts, vx, vy, vz, Axis.X, vx - 1, true);  // +X
        AddSurface(tris, verts, vx, vy, vz, Axis.X, 0, false); // -X
        AddSurface(tris, verts, vx, vy, vz, Axis.Y, vy - 1, true);  // +Y
        AddSurface(tris, verts, vx, vy, vz, Axis.Y, 0, false); // -Y

        _mesh.vertices = verts;
        _mesh.triangles = tris.ToArray();
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
        // UVは必要なら後で。Unlitなら無くてもOK

        // collider
        var mc = GetComponent<MeshCollider>();
        if (addMeshCollider)
        {
            if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = _mesh;
        }
        else
        {
            if (mc) DestroyImmediate(mc);
        }
    }

    enum Axis { X, Y, Z }

    // 指定軸に直交する面を一枚分貼る
    void AddSurface(System.Collections.Generic.List<int> t, Vector3[] v, int vx, int vy, int vz, Axis axis, int fixedIndex, bool forward)
    {
        int sx = (axis == Axis.X) ? vy : vx;
        int sy = (axis == Axis.Y) ? vz : vy;
        int ixMax = sx - 1;
        int iyMax = sy - 1;

        for (int iy = 0; iy < iyMax; iy++)
        {
            for (int ix = 0; ix < ixMax; ix++)
            {
                int i0 = IndexAt(axis, vx, vy, vz, fixedIndex, ix, iy);
                int i1 = IndexAt(axis, vx, vy, vz, fixedIndex, ix + 1, iy);
                int i2 = IndexAt(axis, vx, vy, vz, fixedIndex, ix, iy + 1);
                int i3 = IndexAt(axis, vx, vy, vz, fixedIndex, ix + 1, iy + 1);

                if (forward)
                {
                    t.Add(i0); t.Add(i2); t.Add(i1);
                    t.Add(i1); t.Add(i2); t.Add(i3);
                }
                else
                {
                    t.Add(i0); t.Add(i1); t.Add(i2);
                    t.Add(i1); t.Add(i3); t.Add(i2);
                }
            }
        }
    }

    // グリッド上の (axis固定, u, v) → 3Dインデックス
    int IndexAt(Axis axis, int vx, int vy, int vz, int fixedIndex, int u, int v)
    {
        switch (axis)
        {
            case Axis.Z: // 面はXY、z=fixed
                return (fixedIndex * vy + v) * vx + u;
            case Axis.X: // 面はYZ、x=fixed
                return (v * vy + u) * vx + fixedIndex;
            case Axis.Y: // 面はXZ、y=fixed
                return (v * vy + fixedIndex) * vx + u;
        }
        return 0;
    }
}