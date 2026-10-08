using UnityEngine;
using Leap;

/// <summary>
/// 左手の握り度合い（Openness = 伸びている指の本数 / 5.0）を計算して保持する。
/// パー（全部開く）= 1.0、グー（全部握る）= 0.0
/// 左手が検出されない場合は openness = 1.0（磁力最小 = 安全側）にフォールバックする。
/// </summary>
public class LeftHandController : MonoBehaviour
{
    [Tooltip("シーン内の LeapXRServiceProvider または LeapServiceProvider を割り当てる")]
    public LeapProvider leapProvider;

    [HideInInspector] public float openness = 1.0f;

    [Tooltip("左手が認識されているか（読み取り専用、Inspector でデバッグ確認用）")]
    public bool isLeftHandTracked;

    [Tooltip("伸びている指の本数（読み取り専用）")]
    public int extendedFingerCount;

    void Update()
    {
        Hand leftHand = leapProvider != null ? leapProvider.CurrentFrame?.GetHand(Chirality.Left) : null;

        isLeftHandTracked = leftHand != null;
        if (isLeftHandTracked)
        {
            extendedFingerCount = LeapHandInput.CountExtendedFingers(leftHand);
            openness = extendedFingerCount / 5.0f;
        }
        else
        {
            extendedFingerCount = 0;
            openness = 1.0f;
        }
    }
}
