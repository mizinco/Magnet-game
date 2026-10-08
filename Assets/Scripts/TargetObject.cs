using UnityEngine;

/// <summary>
/// 的のタイプ。
/// </summary>
public enum TargetType
{
    Rare,
    Normal,
    Bad
}

/// <summary>
/// 的にアタッチ。Goal タグへの Trigger 接触で GameManager に通知し自身を消す。
/// パフォーマンス最適化：
///  - OnEnable で TargetRegistry に Rigidbody を登録、OnDisable で解除
///  - GameManager の参照は static キャッシュ（FindFirstObjectByType を 1 回だけ）
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TargetObject : MonoBehaviour
{
    [Tooltip("的のタイプ")]
    public TargetType type = TargetType.Normal;

    [Tooltip("ゴール時に GoalEffect.Spawn を呼ぶか")]
    public bool spawnGoalEffect = true;

    [Tooltip("エフェクトの色（未指定時はマテリアル色を継承）")]
    public Color effectColorOverride = Color.clear;

    // 静的キャッシュ：シーン内で最初に起動した TargetObject が 1 回だけ検索する
    // （シーン再読み込みで破棄されると Unity の null 判定で自動的に再検索される）
    private static GameManager s_gameManager;
    private Rigidbody rb;
    private bool claimed;   // 同フレーム内の多重ゴール判定（Trigger と Collision の両方）を防ぐ

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // 的が有効化されたときは TargetRegistry に登録する
    void OnEnable()
    {
        TargetRegistry.Register(rb);
    }

    // 的が無効化されたときは TargetRegistry からも解除する
    void OnDisable()
    {
        TargetRegistry.Unregister(rb);
    }

    void Start()
    {
        if (s_gameManager == null)
            s_gameManager = FindFirstObjectByType<GameManager>();
    }

    void OnTriggerEnter(Collider other) => TryClaimGoal(other);
    void OnCollisionEnter(Collision collision) => TryClaimGoal(collision.collider);

    // ゴールに触れたときの処理：GameManager に通知してから自分を消す
    private void TryClaimGoal(Collider other)
    {
        if (claimed || other == null || !other.CompareTag("Goal")) return;
        claimed = true;

        if (s_gameManager != null)
            s_gameManager.OnTargetReachedGoal(type);

        if (spawnGoalEffect)
        {
            // effectColorOverride 未指定（透明）ならマテリアル色を継承、取得できなければ赤
            Color color = effectColorOverride;
            if (color.a < 0.01f)
            {
                var mr = GetComponent<MeshRenderer>();
                color = (mr != null && mr.sharedMaterial != null) ? mr.sharedMaterial.color : Color.red;
            }
            GoalEffect.Spawn(transform.position, color);
        }

        Destroy(gameObject);
    }
}
