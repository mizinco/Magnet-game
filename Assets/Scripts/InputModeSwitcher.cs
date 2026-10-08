using UnityEngine;

/// <summary>
/// Sim ↔ Leap の切り替えを 1 箇所で集中管理する。
///
/// 仕組み：
///  - 起動時に Mode に応じて対応する GameObject だけを有効化
///  - 参照を持つコンポーネント（MagneticField, UIController, MagnetVisualFollower）の
///    handInputSource を自動で差し替える
///
/// 実機セッションでやることが「Mode を Leap に変える + Play」だけになる。
/// </summary>
public class InputModeSwitcher : MonoBehaviour
{
    public enum Mode
    {
        Simulated,  // マウス/キーボード
        Leap        // Leap Motion
    }

    [Header("Mode")]
    [Tooltip("実行時に使う入力ソースを切り替える。")]
    public Mode mode = Mode.Simulated;

    [Header("Input Sources")]
    [Tooltip("Mode = Simulated のとき使う")]
    public SimulatedHandInput simulatedHandInput;

    [Tooltip("Mode = Leap のとき使う")]
    public LeapHandInput leapHandInput;

    [Header("Consumers (auto-rewired)")]
    [Tooltip("handInputSource を持つコンポーネントを並べる。実行時に自動差し替え。")]
    public MagneticField magneticField;
    public UIController uiController;

    [Header("Optional")]
    [Tooltip("実機サポートに必要な GameObject 群（LeapServiceProvider 等）。Mode = Simulated のとき無効化される。")]
    public GameObject[] leapOnlyObjects;

    [Tooltip("Mode = Leap のとき無効化する Sim 専用 GameObject（スライダーなど）。")]
    public GameObject[] simulatedOnlyObjects;

    [Header("Runtime State (read-only)")]
    [SerializeField, ReadOnly] private Mode currentMode;

    void Awake()
    {
        ApplyMode();
    }

    void OnValidate()
    {
        // Inspector で変更したときも反映（Play 中のみ）
        if (Application.isPlaying && currentMode != mode)
        {
            ApplyMode();
        }
    }

    /// <summary>
    /// メニューやデバッグキーから呼べる切り替え API
    /// </summary>
    public void SetMode(Mode newMode)
    {
        mode = newMode;
        ApplyMode();
    }

    private void ApplyMode()
    {
        currentMode = mode;
        bool useSim = (mode == Mode.Simulated);

        // 入力 GameObject の有効/無効
        if (simulatedHandInput != null) simulatedHandInput.gameObject.SetActive(useSim);
        if (leapHandInput != null)      leapHandInput.gameObject.SetActive(!useSim);

        SetActiveAll(leapOnlyObjects, !useSim);
        SetActiveAll(simulatedOnlyObjects, useSim);

        // handInputSource の差し替え
        MonoBehaviour chosen = useSim ? simulatedHandInput : leapHandInput;

        if (magneticField != null) magneticField.handInputSource = chosen;
        if (uiController != null)  uiController.handInputSource  = chosen;

        // 磁石モデルはシーン内に複数置かれる可能性があるため、全件を差し替える
        foreach (var follower in FindObjectsByType<MagnetVisualFollower>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            follower.handInputSource = chosen;

        Debug.Log($"[InputModeSwitcher] Mode = {mode}  (active: {(chosen != null ? chosen.name : "NULL")})");
    }

    private static void SetActiveAll(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (var obj in objects)
            if (obj != null) obj.SetActive(active);
    }
}
