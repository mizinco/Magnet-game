using UnityEngine;
using Leap;

/// <summary>
/// 右手の掌位置と「握り判定」を Leap Motion から取得して保持する。
/// LeapHandInput が IHandInput アダプタとしてこの値を公開する。
///
/// 座標変換：Leap の mm 単位の掌位置を 1/5 にスケールし、
/// leapOrigin が割り当てられていればその Transform でワールド座標に変換する。
/// </summary>
public class RightHandController : MonoBehaviour
{
    [Tooltip("LeapServiceProvider を割り当てる。未割り当てなら何もしない")]
    public LeapProvider leapProvider;

    [Tooltip("Leap のローカル座標をワールド座標に変換するための Transform。未割り当てならローカル座標をそのまま使う")]
    public Transform leapOrigin;

    [Tooltip("Leap 座標（mm）をシーン座標に変換する際の除数")]
    public float positionScaleDivisor = 5f;

    [HideInInspector] public Vector3 rightHandPosition;

    [Tooltip("右手が認識されているか（読み取り専用、Inspector でデバッグ確認用）")]
    public bool isRightHandTracked;

    [Tooltip("右手が握られているか（磁石ON/OFFスイッチ）")]
    public bool isRightHandClosed;

    [Tooltip("握り判定の閾値（伸びている指がこの数以下で「握り」と判定）")]
    [Range(0, 5)] public int closedThreshold = 1;

    [Tooltip("伸びている指の本数（読み取り専用）")]
    public int extendedFingerCount;

    void Update()
    {
        if (leapProvider == null) return;

        Hand rightHand = leapProvider.CurrentFrame?.GetHand(Chirality.Right);

        isRightHandTracked = rightHand != null;
        if (!isRightHandTracked)
        {
            isRightHandClosed = false;
            extendedFingerCount = 0;
            return;
        }

        // SDK 7.3.0 では PalmPosition は Vector3 を直接返す
        Vector3 leapLocalPos = rightHand.PalmPosition / positionScaleDivisor;
        rightHandPosition = leapOrigin != null ? leapOrigin.TransformPoint(leapLocalPos) : leapLocalPos;

        extendedFingerCount = LeapHandInput.CountExtendedFingers(rightHand);
        isRightHandClosed = extendedFingerCount <= closedThreshold;
    }
}
