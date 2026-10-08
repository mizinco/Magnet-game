using UnityEngine;

/// <summary>
/// 右手位置に追従させ、右手が握られているときだけ表示する。
/// （A 案：右手グーで磁石が現れる）
/// </summary>
public class MagnetVisualFollower : MonoBehaviour
{
    [Tooltip("IHandInput を実装したコンポーネント")]
    public MonoBehaviour handInputSource;

    private Renderer[] cachedRenderers;

    /// <summary>InputModeSwitcher が実行時に差し替えるため、キャッシュせず毎回キャストする。</summary>
    private IHandInput HandInput => handInputSource as IHandInput;

    void Start()
    {
        // 非アクティブな子も含めて Renderer をキャッシュ
        cachedRenderers = GetComponentsInChildren<Renderer>(true);

        // 保険：未結線時にシーンから検索
        if (handInputSource == null)
        {
            handInputSource = FindFirstObjectByType<SimulatedHandInput>();
            if (handInputSource == null) handInputSource = FindFirstObjectByType<LeapHandInput>();
            if (handInputSource != null)
                Debug.LogWarning("[MagnetVisualFollower] handInputSource 未結線、自動検索で接続しました。");
        }
    }

    void Update()
    {
        var handInput = HandInput;
        if (handInput == null) return;

        transform.position = handInput.RightHandPosition;
        SetVisible(handInput.IsRightHandClosed);
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in cachedRenderers)
            if (r != null && r.enabled != visible) r.enabled = visible;
    }
}
