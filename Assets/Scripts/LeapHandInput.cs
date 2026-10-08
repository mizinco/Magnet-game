using UnityEngine;
using Leap;

/// <summary>
/// 実機 Leap Motion 用の IHandInput アダプタ。
/// RightHandController と LeftHandController を参照して、
/// IHandInput インターフェースで公開する。
///
/// シーン上は LeapHandInput GameObject を 1 つ作って、
/// RightHandController と LeftHandController を割り当てる。
/// </summary>
public class LeapHandInput : MonoBehaviour, IHandInput
{
    [Tooltip("Leap から右手位置を取得するコンポーネント")]
    public RightHandController rightHandController;

    [Tooltip("Leap から左手 Openness を計算するコンポーネント")]
    public LeftHandController leftHandController;

    public Vector3 RightHandPosition =>
        rightHandController != null ? rightHandController.rightHandPosition : Vector3.zero;

    public bool IsRightHandTracked =>
        rightHandController != null && rightHandController.isRightHandTracked;

    // Openness は 0=グー〜1=パー の値。左手コントローラ未割り当て時は 1.0（磁力最小）とみなす。
    public float Openness =>
        leftHandController != null ? leftHandController.openness : 1.0f;

    public bool IsLeftHandTracked =>
        leftHandController != null && leftHandController.isLeftHandTracked;

    public bool IsRightHandClosed =>
        rightHandController != null && rightHandController.isRightHandClosed;

    void Awake()
    {
        if (rightHandController == null || leftHandController == null)
        {
            Debug.LogWarning("[LeapHandInput] RightHandController または LeftHandController が未割り当てです。");
        }
    }

    /// <summary>伸びている指の本数を数える（Right/LeftHandController 共通）</summary>
    public static int CountExtendedFingers(Hand hand)
    {
        int extended = 0;
        foreach (Finger finger in hand.fingers)
            if (finger.IsExtended) extended++;
        return extended;
    }
}
