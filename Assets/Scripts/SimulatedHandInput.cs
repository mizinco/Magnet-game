using UnityEngine;

/// <summary>
/// Leap Motion がない環境で右手位置・Openness・右手握りをエミュレートする。
/// 操作：
///   - マウス：カーソル位置を XY 平面に投影して右手位置に
///   - マウス左クリック中：右手「握り」状態（磁石ON）
///   - WASD：右手位置を増分で動かす
///   - Q/E キー：左手 Openness を 0.0〜1.0 で増減（磁力強度）
/// </summary>
public class SimulatedHandInput : MonoBehaviour, IHandInput
{
    [Header("Input Sources (which input wins)")]
    public bool useMouse = true;
    public bool useKeyboard = true;
    public bool useKeyboardForOpenness = true;

    [Tooltip("マウス左クリックで右手を「握り」状態（磁石ON）にする")]
    public bool useMouseForClose = true;

    [Header("Camera Mapping")]
    public Camera targetCamera;
    public float planeZ = 0f;

    [Header("Keyboard Settings")]
    public float keyboardMoveSpeed = 4f;
    public float opennessKeySpeed = 1.5f;

    [Header("Initial State")]
    public Vector3 initialPosition = Vector3.zero;
    [Range(0f, 1f)] public float initialOpenness = 1.0f;

    [Header("Field Bounds (磁石が出られる範囲、BoundaryWalls と一致)")]
    public Vector3 fieldMin = new Vector3(-4.5f, -4.5f, -1.5f);
    public Vector3 fieldMax = new Vector3( 4.5f,  4.5f,  1.5f);

    // --- IHandInput 実装 ---
    public Vector3 RightHandPosition => currentPosition;
    public bool IsRightHandTracked => true;
    public float Openness => currentOpenness;
    public bool IsLeftHandTracked => true;
    public bool IsRightHandClosed => currentRightClosed;

    private Vector3 currentPosition;
    private float currentOpenness;
    private bool currentRightClosed;

    void Start()
    {
        currentPosition = initialPosition;
        currentOpenness = Mathf.Clamp01(initialOpenness);
        if (targetCamera == null) targetCamera = Camera.main;
    }

    void Update()
    {
        // === 右手位置の更新 ===
        if (useMouse && targetCamera != null)
        {
            Vector3 mouseScreen = Input.mousePosition;
            mouseScreen.z = Mathf.Abs(targetCamera.transform.position.z - planeZ);
            Vector3 world = targetCamera.ScreenToWorldPoint(mouseScreen);
            world.z = planeZ;
            currentPosition = world;
        }

        if (useKeyboard)
        {
            float dx = 0f, dy = 0f;
            if (Input.GetKey(KeyCode.A)) dx -= 1f;
            if (Input.GetKey(KeyCode.D)) dx += 1f;
            if (Input.GetKey(KeyCode.W)) dy += 1f;
            if (Input.GetKey(KeyCode.S)) dy -= 1f;
            if (dx != 0f || dy != 0f)
                currentPosition += new Vector3(dx, dy, 0f) * keyboardMoveSpeed * Time.deltaTime;
        }

        // フィールド外に磁石が出ないように Clamp
        currentPosition.x = Mathf.Clamp(currentPosition.x, fieldMin.x, fieldMax.x);
        currentPosition.y = Mathf.Clamp(currentPosition.y, fieldMin.y, fieldMax.y);
        currentPosition.z = Mathf.Clamp(currentPosition.z, fieldMin.z, fieldMax.z);

        // === 左手 Openness（磁力強度）===
        if (useKeyboardForOpenness)
        {
            if (Input.GetKey(KeyCode.Q)) currentOpenness -= opennessKeySpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.E)) currentOpenness += opennessKeySpeed * Time.deltaTime;
            currentOpenness = Mathf.Clamp01(currentOpenness);
        }

        // === 右手 握り（マウス左クリック中 = ON）===
        if (useMouseForClose)
        {
            currentRightClosed = Input.GetMouseButton(0);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = currentRightClosed ? Color.red : Color.magenta;
        Gizmos.DrawWireSphere(Application.isPlaying ? currentPosition : initialPosition, 0.2f);
    }
}
