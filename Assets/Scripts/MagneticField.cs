using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 磁力フィールドの中心位置・力・範囲を毎フレーム計算し、
/// 範囲内にある的（Target）に磁力を加える。
///
/// 入力：
///   - 右手の位置        … 磁場の中心位置（IHandInput.RightHandPosition）
///   - 左手の Openness   … 磁力の強さと到達範囲を決める（IHandInput.Openness）
///   - 右手の握り        … 磁石 ON/OFF スイッチ（IHandInput.IsRightHandClosed）
///
/// 物理計算：
///   各的に対して、
///     力 = baseMagneticForce × forceFactor(openness) × attenuation(distance, range)
///   を方向ベクトル「右手位置 - 的位置」と掛けて Rigidbody.AddForce する。
///   attenuation は (1 - distance/range) の線形減衰。
///
/// 可視化：
///   XY/YZ/XZ 平面の 3 本のリングを LineRenderer で描き、
///   磁場が球状に広がっている様子を表現する。
///
/// パフォーマンス上の工夫：
///   - 的のリストは TargetRegistry（静的）から取得し、毎フレームの
///     FindGameObjectsWithTag を避ける
///   - 単位円の cos/sin を事前計算し、LineRenderer へは SetPositions で一括転送する
/// </summary>
public class MagneticField : MonoBehaviour
{
    // ────────────────────────────────────────────
    // Inspector 公開フィールド
    // ────────────────────────────────────────────

    [Header("Input")]
    [Tooltip("IHandInput を実装した GameObject。LeapHandInput または SimulatedHandInput を割り当てる")]
    public MonoBehaviour handInputSource;

    [Header("Game State (auto-detected)")]
    [Tooltip("GameManager。未割り当てなら自動検索される")]
    public GameManager gameManager;

    [Header("Visualization")]
    [Tooltip("XY 平面のリング")]
    public LineRenderer fieldLineXY;

    [Tooltip("YZ 平面のリング")]
    public LineRenderer fieldLineYZ;

    [Tooltip("XZ 平面のリング")]
    public LineRenderer fieldLineXZ;

    [Tooltip("リング 1 本あたりの頂点数。小さいほど軽量、大きいほど滑らか")]
    [Range(16, 96)] public int circleSegments = 48;

    [Header("Physics Parameters")]
    [Tooltip("基本磁力。範囲内の的に与える力の基準値（推奨 10〜20）")]
    public float baseMagneticForce = 12.0f;

    [Tooltip("Rigidbody.AddForce に渡す ForceMode")]
    public ForceMode forceMode = ForceMode.Force;

    // ────────────────────────────────────────────
    // openness → 磁場パラメータの変換（UIController からも参照する）
    // ────────────────────────────────────────────

    /// <summary>磁場の到達範囲。パー（1.0）で 0.5、グー（0.0）で 3.0。</summary>
    public static float FieldRange(float openness) => 0.5f + (1.0f - openness) * 2.5f;

    /// <summary>磁力の倍率。パー（1.0）で 1.0、グー（0.0）で 1.5。</summary>
    public static float ForceFactor(float openness) => 1.0f + (1.0f - openness) * 0.5f;

    // ────────────────────────────────────────────
    // 内部状態
    // ────────────────────────────────────────────

    /// <summary>
    /// InputModeSwitcher が実行時に handInputSource を差し替えるため、キャッシュせず毎回キャストする。
    /// </summary>
    private IHandInput HandInput => handInputSource as IHandInput;

    private enum Plane { XY, YZ, XZ }

    private Vector2[] unitCircle;   // 単位円上の (cos, sin)。circleSegments 変更時に再計算
    private Vector3[] ringBuffer;   // LineRenderer.SetPositions 用の作業領域

    // ────────────────────────────────────────────
    // ライフサイクル
    // ────────────────────────────────────────────

    void Start()
    {
        if (handInputSource != null && HandInput == null)
            Debug.LogError($"[MagneticField] {handInputSource.name} は IHandInput を実装していません。");

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        foreach (var lr in new[] { fieldLineXY, fieldLineYZ, fieldLineXZ })
            if (lr != null) lr.useWorldSpace = true;
    }

    /// <summary>磁石が有効か：ゲーム進行中 かつ 右手が握られている（A 案）</summary>
    private bool IsMagnetActive(IHandInput input)
    {
        bool gameActive = gameManager == null || gameManager.IsPlaying;
        return gameActive && input.IsRightHandClosed;
    }

    // ────────────────────────────────────────────
    // 物理計算（FixedUpdate で毎物理ステップ実行）
    // ────────────────────────────────────────────

    void FixedUpdate()
    {
        var input = HandInput;
        if (input == null || !IsMagnetActive(input)) return;

        Vector3 handPos   = input.RightHandPosition;
        float openness    = input.Openness;
        float fieldRange  = FieldRange(openness);
        float forceScale  = baseMagneticForce * ForceFactor(openness);

        List<Rigidbody> list = TargetRegistry.ActiveTargets;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = list[i];
            // 解放済みの Rigidbody を遅延削除
            if (rb == null) { list.RemoveAt(i); continue; }

            Vector3 toHand = handPos - rb.position;
            float distance = toHand.magnitude;

            // 範囲外、もしくは中心に近すぎる的（0除算回避）はスキップ
            if (distance > fieldRange || distance < 0.001f) continue;

            // 距離による線形減衰（中心で 1.0、範囲端で 0.0）
            float attenuation = 1.0f - (distance / fieldRange);
            rb.AddForce(toHand / distance * (forceScale * attenuation), forceMode);
        }
    }

    // ────────────────────────────────────────────
    // 可視化（Update で毎フレーム描画位置を更新）
    // ────────────────────────────────────────────

    void Update()
    {
        var input = HandInput;
        if (input == null) return;

        float openness = input.Openness;
        Vector3 center = input.RightHandPosition;
        float radius   = FieldRange(openness);
        Color color    = GetFieldColor(IsMagnetActive(input), openness);

        EnsureCircleCache();
        DrawRing(fieldLineXY, center, radius, Plane.XY, color);
        DrawRing(fieldLineYZ, center, radius, Plane.YZ, color);
        DrawRing(fieldLineXZ, center, radius, Plane.XZ, color);
    }

    private void EnsureCircleCache()
    {
        int segments = Mathf.Max(8, circleSegments);
        if (unitCircle != null && unitCircle.Length == segments + 1) return;

        unitCircle = new Vector2[segments + 1];
        ringBuffer = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * 2f * Mathf.PI;
            unitCircle[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }

    /// <summary>指定の平面上に半径 radius の円を LineRenderer で描く。</summary>
    private void DrawRing(LineRenderer lr, Vector3 center, float radius, Plane plane, Color color)
    {
        if (lr == null) return;

        for (int i = 0; i < unitCircle.Length; i++)
        {
            float c = radius * unitCircle[i].x;
            float s = radius * unitCircle[i].y;
            ringBuffer[i] = plane switch
            {
                Plane.XY => new Vector3(center.x + c, center.y + s, center.z),
                Plane.YZ => new Vector3(center.x, center.y + c, center.z + s),
                _        => new Vector3(center.x + c, center.y, center.z + s),
            };
        }

        lr.positionCount = ringBuffer.Length;
        lr.SetPositions(ringBuffer);
        lr.startColor = color;
        lr.endColor   = color;
    }

    /// <summary>
    /// 磁場の色を状況に応じて返す。
    ///   OFF          : 灰色（半透明）
    ///   グー（強い） : 赤
    ///   中間         : 黄
    ///   パー（弱い） : 緑
    /// </summary>
    private static Color GetFieldColor(bool active, float openness)
    {
        if (!active)         return new Color(0.5f, 0.5f, 0.5f, 0.3f);
        if (openness < 0.3f) return new Color(1f,   0.2f, 0.2f, 0.8f);
        if (openness < 0.7f) return new Color(1f,   0.9f, 0.2f, 0.8f);
        return                      new Color(0.3f, 1f,   0.4f, 0.5f);
    }
}
