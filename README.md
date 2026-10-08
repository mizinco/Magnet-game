# Magnet

Leap Motion で両手を使って「磁石」を操り、的を土管（ゴール）に運び入れる 60 秒スコアアタックゲーム。

<!-- TODO: プレイ動画の GIF / YouTube リンクを置く（ポートフォリオで最も見られる部分） -->
<!-- ![demo](docs/demo.gif) -->

## 遊び方

| 手 | 操作 | 効果 |
|---|---|---|
| 右手 | 位置を動かす | 磁石（磁場の中心）が追従 |
| 右手 | グー / パー | 磁石 ON / OFF |
| 左手 | 開閉 | 磁力の強さと範囲（パー＝弱く狭い、グー＝強く広い） |

的は 銀（Normal）+10 / 金（Rare）+30 / 赤（Bad）−20。時間経過で赤の出現率が上がる。
上位 5 件はローカルランキングに名前付きで保存される。

Leap Motion がなくても、マウス（位置・左クリックで磁石 ON）と Q/E キー（磁力）で遊べる。

| キー | 動作 |
|---|---|
| Space | スタート |
| R | 終了後にリスタート |
| Shift+R | いつでも強制リセット |
| Shift+Backspace | ベスト記録・ランキングを消去 |

## 動作環境

- Unity 2022.3.62f3
- [Ultraleap Tracking](https://github.com/ultraleap/UnityPlugin) 7.3.0（OpenUPM 経由で自動取得）
- Leap Motion Controller（任意。なくてもシミュレーション入力で動く）

`Assets/Scenes/Main.unity` を開いて Play。`InputModeSwitcher` の Mode を `Leap` にすると実機入力になる。

## 設計

```
IHandInput ─┬─ LeapHandInput ── RightHandController / LeftHandController (Leap SDK)
            └─ SimulatedHandInput (マウス・キーボード)
                 ▲
InputModeSwitcher が起動時にどちらか一方を MagneticField / UIController / MagnetVisualFollower に注入

MagneticField ──(FixedUpdate)──> TargetRegistry.ActiveTargets に AddForce
TargetObject ──(Goal 接触)──> GameManager.OnTargetReachedGoal ──> TargetSpawner.RequestSpawn
GameManager ──> UIController / TutorialPanel / GameClearPanelController / Ranking
```

- **入力の抽象化**：`IHandInput` により、Leap 実機とマウス入力を利用側のコード変更なしで切り替えられる。実機がない環境でも開発・デバッグできる。
- **磁力モデル**：`力 = 基本磁力 × 倍率(openness) × (1 − 距離/範囲)` の線形減衰。範囲と倍率は左手の開き具合から決まる。
- **パフォーマンス**：的は `TargetRegistry`（静的リスト）に自己登録し、毎フレームの `FindGameObjectsWithTag` を避けている。磁場リングは単位円を事前計算して `SetPositions` で一括更新している。
- **エディタ拡張**：`Tools > Magnet > Setup Everything` を実行すると、シーンの構成（カメラ・UI・入力・壁・ゴール）を自動で組み立てる（`Assets/Scripts/Editor/SceneSetupTool.cs`）。

## クレジット
<!-- TODO: チーム制作の場合はメンバーと自分の担当範囲を書く -->
- Ultraleap Tracking Plugin は Ultraleap 社のもので、本リポジトリには含まない（Package Manager 経由で取得）。ライセンスは同プラグインに従う

## ライセンス

[MIT License](LICENSE)（Ultraleap Tracking Plugin など第三者のパッケージは除く）
