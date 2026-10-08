using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 現在シーンに存在する Target の Rigidbody を保持する静的レジストリ。
/// MagneticField はこのリストを直接走査することで FindGameObjectsWithTag を回避し、
/// 物理ステップごとのコストを定数化する。
///
/// TargetObject が OnEnable で Register、OnDisable で Unregister する。
/// </summary>
public static class TargetRegistry
{
    /// <summary>外部からは読み取り専用として扱う（null 要素の遅延削除のみ MagneticField が行う）。</summary>
    public static readonly List<Rigidbody> ActiveTargets = new List<Rigidbody>(64);

    public static void Register(Rigidbody rb)
    {
        if (rb == null) return;
        if (!ActiveTargets.Contains(rb)) ActiveTargets.Add(rb);
    }

    public static void Unregister(Rigidbody rb)
    {
        if (rb == null) return;
        ActiveTargets.Remove(rb);
    }

    public static void Clear()
    {
        ActiveTargets.Clear();
    }

    // Domain Reload を無効化した Play Mode でも前回の残骸を持ち越さないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => ActiveTargets.Clear();
}
