using UnityEngine;

/// <summary>
/// ScoreAttack 用の的生成器。形状は球体に統一。
/// 開始時に initialSpawn 個を配置、以後ゴール 1 個ごとに 1 個追加生成する。
/// Normal のみ色とサイズをわずかに揺らして単調さを回避する。
/// </summary>
public class TargetSpawner : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Hierarchy 上の Targets ルート（未設定なら自動生成）")]
    public Transform targetsRoot;

    [Tooltip("ゲーム開始時に最初に生成する的の数")]
    public int initialSpawn = 8;

    [Tooltip("Rare 的の出現率 0.0〜1.0")]
    [Range(0f, 1f)] public float rareRate = 0.1f;

    [Tooltip("Bad 的の出現率 0.0〜1.0")]
    [Range(0f, 1f)] public float badRate = 0.3f;

    [Header("Spawn Range")]
    [Tooltip("的が生成される X 範囲。左半分のみ。右側は土管（ゴール）用に空ける")]
    public float xMin = -4f, xMax = -1f;
    public float yMin = -4f, yMax = 4f;

    [Tooltip("Z 軸ランダム化の幅")]
    public float zRange = 0.2f;

    [Header("Materials")]
    public Material normalMaterial;
    public Material rareMaterial;
    public Material badMaterial;

    [Tooltip("的の基本サイズ（CreatePrimitive Sphere は直径 1.0、 0.4〜0.8）")]
    [Range(0.1f, 1f)] public float baseScale = 0.6f;

    [Header("Normal 的のバリエーション")]
    [Range(0f, 0.2f)] public float normalHueJitter = 0.04f;
    [Range(0f, 0.3f)] public float normalValueJitter = 0.1f;
    [Range(0.5f, 1f)] public float normalScaleMin = 0.9f;
    [Range(1f, 2f)]   public float normalScaleMax = 1.15f;

    [Header("Avoid Goal")]
    [Tooltip("土管（ゴール）の位置。この周囲には goalAvoidRadius 以内には沸かせない")]
    public Vector3 goalPosition = new Vector3(4, 0, 0);
    public float goalAvoidRadius = 2.0f;

    [Header("Runtime State (read-only)")]
    [SerializeField, ReadOnly] private bool spawning = false;
    [SerializeField, ReadOnly] private int spawnCount = 0;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId     = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock mpb;

    // Inspector での設定漏れがあっても、ゲーム開始前に自動で補完する
    void Awake() { EnsureFallbacks(); }

    /// <summary>
    /// 必要なオブジェクトや設定が Inspector で割り当てられていないときに、
    /// 代替のオブジェクトを探して割り当てる。
    /// </summary>
    private void EnsureFallbacks()
    {
        if (targetsRoot == null)
        {
            var root = GameObject.Find("Targets");
            if (root == null)
            {
                root = new GameObject("Targets");
                Debug.LogWarning("[TargetSpawner] 'Targets' を自動生成しました。");
            }
            targetsRoot = root.transform;
        }
        if (normalMaterial == null) Debug.LogError("[TargetSpawner] normalMaterial 未設定");
        if (rareMaterial   == null) Debug.LogError("[TargetSpawner] rareMaterial 未設定");
        if (badMaterial    == null) Debug.LogError("[TargetSpawner] badMaterial 未設定");
    }

    /// <summary>
    /// ゲーム開始前にこれを呼び出すと、initialSpawn 個の的を生成してスポーンを開始する。
    /// </summary>
    public void BeginSpawning()
    {
        EnsureFallbacks();
        spawning = true;
        spawnCount = 0;

        // シーンに事前配置された的を片付ける（実行中は DestroyImmediate ではなく Destroy を使う）
        if (targetsRoot != null)
        {
            for (int i = targetsRoot.childCount - 1; i >= 0; i--)
            {
                var child = targetsRoot.GetChild(i).gameObject;
                child.SetActive(false);   // 即座に TargetRegistry から外す
                Destroy(child);
            }
        }

        for (int i = 0; i < initialSpawn; i++) SpawnOne();

        Debug.Log($"[TargetSpawner] BeginSpawning: 初期 {initialSpawn} 個を配置。");
    }

    /// <summary>
    /// スポーンを停止する。以後、RequestSpawn を呼び出しても何もしなくなる。
    /// ただし、すでにスポーンした的はそのまま残る。
    /// 必要に応じて TargetRegistry.Clear() などで消すこと
    /// </summary>
    public void StopSpawning()
    {
        spawning = false;
    }

    /// <summary>
    /// スポーン中なら新しい的を 1 個生成する。ゴールに触れたときなど、GameManager から呼び出す想定。
    /// </summary>
    public void RequestSpawn()
    {
        if (!spawning) return;
        SpawnOne();
    }

    /// <summary>
    /// 的を 1 個生成する。タイプと位置はランダム抽選。
    /// </summary>
    private void SpawnOne()
    {
        // タイプ抽選
        float roll = Random.value;
        TargetType type;
        if      (roll < rareRate)              type = TargetType.Rare;
        else if (roll < rareRate + badRate)    type = TargetType.Bad;
        else                                   type = TargetType.Normal;

        // 位置抽選（ゴール周辺を避ける）
        Vector3 pos = Vector3.zero;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            pos = new Vector3(
                Random.Range(xMin, xMax),
                Random.Range(yMin, yMax),
                Random.Range(-zRange, zRange));
            if (Vector3.Distance(new Vector3(pos.x, pos.y, 0), goalPosition) >= goalAvoidRadius)
                break;
        }

        // 球体生成（SphereCollider と MeshRenderer 付き）。名前にはデバッグ用の連番を付ける
        var spawned = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spawned.tag = "Target";
        spawned.name = type switch
        {
            TargetType.Bad  => $"BadTarget_{spawnCount:00}",
            TargetType.Rare => $"RareTarget_{spawnCount:00}",
            _               => $"Target_{spawnCount:00}",
        };
        if (targetsRoot != null) spawned.transform.SetParent(targetsRoot);
        spawned.transform.position = pos;

        // 基本サイズ。Normal のみサイズをわずかに揺らして単調さを回避する
        float scale = baseScale;
        if (type == TargetType.Normal) scale *= Random.Range(normalScaleMin, normalScaleMax);
        spawned.transform.localScale = Vector3.one * scale;

        // 無重力で、空気抵抗を強めにして磁力を受けたときの動きを安定させる
        var rb = spawned.AddComponent<Rigidbody>();
        rb.useGravity  = false;
        rb.drag        = 1.5f;
        rb.angularDrag = 2.0f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;

        // [RequireComponent(Rigidbody)] のため、必ず Rigidbody 付与後に追加する
        var to = spawned.AddComponent<TargetObject>();
        to.type = type;

        // マテリアル割当
        Material mat = type switch
        {
            TargetType.Bad  => badMaterial,
            TargetType.Rare => rareMaterial,
            _               => normalMaterial,
        };

        var renderer = spawned.GetComponent<MeshRenderer>();
        if (renderer != null && mat != null)
        {
            renderer.sharedMaterial = mat;

            // Normal のみ MaterialPropertyBlock で色をわずかに揺らす（マテリアルは共有のまま）
            if (type == TargetType.Normal && (normalHueJitter > 0f || normalValueJitter > 0f))
            {
                mpb ??= new MaterialPropertyBlock();
                mpb.Clear();

                // ベースカラー（_BaseColor → _Color → 白）に HSV ジッタを加える。アルファは継承
                bool hasBase  = mat.HasProperty(BaseColorId);
                bool hasColor = mat.HasProperty(ColorId);
                Color baseCol = hasBase  ? mat.GetColor(BaseColorId)
                              : hasColor ? mat.GetColor(ColorId)
                              : Color.white;
                Color.RGBToHSV(baseCol, out float h, out float sat, out float v);
                h = Mathf.Repeat(h + Random.Range(-normalHueJitter, normalHueJitter), 1f);
                v = Mathf.Clamp01(v + Random.Range(-normalValueJitter, normalValueJitter));
                Color jittered = Color.HSVToRGB(h, sat, v);
                jittered.a = baseCol.a;

                if (hasBase)  mpb.SetColor(BaseColorId, jittered);
                if (hasColor) mpb.SetColor(ColorId, jittered);
                renderer.SetPropertyBlock(mpb);
            }
        }

        spawnCount++;
    }
}
