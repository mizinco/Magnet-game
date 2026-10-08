using UnityEngine;

/// <summary>
/// 手の入力ソースを抽象化するインタフェース。
///
/// 設計意図：
///   Leap Motion 実機を使う場合と、開発時に Leap Motion がない環境で
///   マウス・キーボードによる代替入力を使う場合とで、
///   利用側のコード（MagneticField, UIController, MagnetVisualFollower 等）を
///   一切書き換えずに済むようにするために導入したインタフェース。
///
/// 実装クラス：
///   - LeapHandInput        … 実機 Leap Motion 経由で値を取得する
///   - SimulatedHandInput   … マウス・キーボードでエミュレートする
///
/// 利用側はこのインタフェース越しに右手位置・左手 Openness・右手の握りを取り出す。
/// 実機↔シミュレータの切り替えは Hierarchy 上で対応する GameObject を
/// アクティブにするだけで済む（InputModeSwitcher が一括で管理する）。
/// </summary>
public interface IHandInput
{
    /// <summary>
    /// 右手の掌のワールド座標（Unity 単位）。
    /// 磁石（磁場の中心）の位置として利用される。
    /// </summary>
    Vector3 RightHandPosition { get; }

    /// <summary>
    /// 右手が認識されているか。
    /// Leap モードでは Leap センサーが右手を検出している場合のみ true。
    /// Simulated モードでは常に true。
    /// </summary>
    bool IsRightHandTracked { get; }

    /// <summary>
    /// 左手の開き度合い 0.0〜1.0。
    /// 1.0 = パー（全部の指が伸びている）→ 磁力最小・磁場範囲狭
    /// 0.0 = グー（全部の指が握られている）→ 磁力最大・磁場範囲広
    /// 左手が検出されていない場合は安全側の 1.0（磁力最小）にフォールバックする。
    /// </summary>
    float Openness { get; }

    /// <summary>
    /// 左手が認識されているか。
    /// Leap モードでは Leap センサーが左手を検出している場合のみ true。
    /// Simulated モードでは常に true。
    /// </summary>
    bool IsLeftHandTracked { get; }

    /// <summary>
    /// 右手が「握り」状態かどうか。A 案（磁石 ON/OFF スイッチ）として使う。
    /// true  = グー（磁石 ON、的に磁力が働く）
    /// false = パー（磁石 OFF、磁力ゼロ・磁石モデルも非表示）
    /// Leap モードでは伸びている指の本数で判定（既定では 1 本以下で握り）。
    /// Simulated モードではマウス左クリック中のみ true。
    /// </summary>
    bool IsRightHandClosed { get; }
}
