using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Canvas 上の UI Text を更新するクラス
/// </summary>
public class UIController : MonoBehaviour
{
    [Header("Input Source")]
    [Tooltip("IHandInput を実装したコンポーネント")]
    public MonoBehaviour handInputSource;

    /// <summary>InputModeSwitcher が実行時に差し替えるため、キャッシュせず毎回キャストする。</summary>
    private IHandInput HandInput => handInputSource as IHandInput;

    [Header("Hand Status")]
    public Text opennessText;        // 左手 Openness
    public Text rangeText;           // 磁力フィールド範囲
    public Text forceText;           // 磁力強度倍率
    public Text rightHandPositionText;  // 右手位置（デバッグ用）

    [Header("Score & Time")]
    public Text scoreText;           // スコア表示
    public Text timeText;            // 経過時間
    public Text remainingTimeText;   // 残り時間
    public Text bestScoreText;       // ベストスコア

    [Header("Game State")]
    public Text pressSpaceText;      // "Press SPACE to Start"
    public Text gameOverText;        // "TIME UP" / "GAME OVER"

    [Header("Tutorial")]
    [Tooltip("Idle 中に表示する操作説明パネル。SetGameState から表示切替する。")]
    public TutorialPanel tutorialPanel;

    void Start()
    {
        if (handInputSource != null && HandInput == null)
            Debug.LogError($"[UIController] {handInputSource.name} は IHandInput を実装していません。");

        // 保険：tutorialPanel が Inspector で未結線でもシーン内から探す
        if (tutorialPanel == null)
        {
            tutorialPanel = FindFirstObjectByType<TutorialPanel>();
            if (tutorialPanel != null)
                Debug.LogWarning("[UIController] tutorialPanel 未結線でしたが、自動検索で接続しました。Inspector で割り当ててください。");
            else
                Debug.LogError("[UIController] TutorialPanel がシーンに存在しません。Tools/Magnet/Individual/Generate Tutorial Panel を実行してください。");
        }

        // UI の初期化
        if (scoreText != null)          scoreText.text = "Score: 0";
        if (timeText != null)           timeText.text = "Time: 0.00s";
        if (remainingTimeText != null)  remainingTimeText.text = "Remain: 60.0s";
        if (bestScoreText != null)      bestScoreText.text = "Best: 0";
        if (pressSpaceText != null)     pressSpaceText.text = "Press SPACE to Start";
        if (gameOverText != null)       gameOverText.text = "";
    }

    // 毎フレーム、手入力の状態を読み取って UI に反映する
    void Update()
    {
        var handInput = HandInput;
        if (handInput == null) return;

        // 磁力フィードバック表示
        float openness = handInput.Openness;
        if (opennessText != null)
            opennessText.text = $"Openness: {openness:F2}";
        if (rangeText != null)
            rangeText.text = $"Range: {MagneticField.FieldRange(openness):F2}";
        if (forceText != null)
            forceText.text = $"Force: x{MagneticField.ForceFactor(openness):F2}";

        // 右手位置表示（デバッグ用）
        if (rightHandPositionText != null)
        {
            if (handInput.IsRightHandTracked)
            {
                Vector3 p = handInput.RightHandPosition;
                rightHandPositionText.text = $"R-Hand: ({p.x:F1}, {p.y:F1}, {p.z:F1})";
            }
            else
            {
                rightHandPositionText.text = "R-Hand: (not tracked)";
            }
        }
    }

    // ===== GameManager から呼ばれるやつ =====

    // スコアを更新する
    public void SetScore(int score)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
    }

    // 経過時間の表示
    public void SetTime(float seconds)
    {
        if (timeText != null)
            timeText.text = $"Time: {seconds:F2}s";
    }

    // 残り時間を更新する
    public void SetRemainingTime(float seconds)
    {
        if (remainingTimeText == null) return;
        if (seconds > 0f)
            remainingTimeText.text = $"Remain: {seconds:F1}s";
        else
            remainingTimeText.text = "TIME UP";
    }

    // ベストスコアを更新する
    public void SetBestScore(int score)
    {
        if (bestScoreText != null)
            bestScoreText.text = $"Best: {score}";
    }

    // ゲームの状態に応じて UI を切り替える
    public void SetGameState(bool started, bool finished)
    {
        if (pressSpaceText != null)
            pressSpaceText.text = (!started && !finished) ? "Press SPACE to Start" : "";

        if (gameOverText != null)
            gameOverText.text = finished ? "TIME UP\n(Press R to Restart)" : "";

        // チュートリアルパネル：Idle のときだけ表示
        if (tutorialPanel != null)
            tutorialPanel.ApplyGameState(started, finished);
    }
}
