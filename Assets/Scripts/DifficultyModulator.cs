using UnityEngine;

/// <summary>
/// ゲーム進行に応じて難易度パラメータを段階的に変更する。
///
/// ScoreAttack の 60 秒を 3 フェーズに分割：
///   0～20秒  ：導入フェーズ（ターゲット少ない、赤ターゲット少なめ）
///  20～40秒  ：中盤フェーズ（ターゲット増加、判断要素追加）
///  40～60秒  ：終盤フェーズ（高密度、リスク高い）
/// </summary>
public class DifficultyModulator : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;   // フェーズ判定用
    public TargetSpawner spawner;     // 難易度パラメータの変更先

    [Header("Phase 1: Introduction (0～20s)")]
    [Tooltip("この期間の Bad ターゲット出現率")]
    [Range(0f, 1f)] public float phase1BadRate = 0.15f;

    [Header("Phase 2: Middle (20～40s)")]
    [Range(0f, 1f)] public float phase2BadRate = 0.25f;

    [Header("Phase 3: Climax (40～60s)")]
    [Range(0f, 1f)] public float phase3BadRate = 0.35f;

    [Header("Debug")]
    [SerializeField, ReadOnly] private int currentPhase = 0;
    [SerializeField, ReadOnly] private float currentBadRate = 0.15f;

    void Start()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (spawner == null)     spawner     = FindFirstObjectByType<TargetSpawner>();
    }

    // ゲームの経過時間に応じて難易度を段階的に変更する
    void Update()
    {
        if (gameManager == null || !gameManager.IsPlaying) return;

        int newPhase = GetPhase(gameManager.ElapsedTime);
        if (newPhase != currentPhase)
        {
            currentPhase = newPhase;
            ApplyPhase(newPhase);
        }
    }

    // 経過時間に応じて現在のフェーズ（1〜3）を判定する
    private static int GetPhase(float elapsedTime)
    {
        if (elapsedTime < 20f) return 1;
        if (elapsedTime < 40f) return 2;
        return 3;
    }

    // フェーズに応じて Spawner の Bad 出現率を更新する
    private void ApplyPhase(int phase)
    {
        if (spawner == null) return;

        (float rate, string label) = phase switch
        {
            1 => (phase1BadRate, "Intro"),
            2 => (phase2BadRate, "Middle"),
            _ => (phase3BadRate, "Climax"),
        };

        currentBadRate  = rate;
        spawner.badRate = rate;
        Debug.Log($"[DifficultyModulator] Phase {phase} ({label}) - BadRate={rate:F2}");
    }
}
