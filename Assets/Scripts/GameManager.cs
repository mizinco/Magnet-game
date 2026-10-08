using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ゲーム全体の状態を管理する中心クラス（Score Attack モード）。
///
/// 状態遷移：
///   Idle      … タイトル表示中。Space キーで Playing へ
///   Playing   … 制限時間 timeLimit（既定 60 秒）が経過するまで
///   Finished  … 制限時間切れ。GameClearPanel を表示、R キーでリスタート
///
/// 役割：
///   - 経過時間・残り時間の管理
///   - スコア計算（的のタイプごとに加減算）
///   - ベストスコアの保存（PlayerPrefs）
///   - TargetSpawner への BeginSpawning / StopSpawning 指示
///   - GameClearPanelController を通じたランキング表示
///   - リセット系のショートカット入力（Shift+R / Shift+Backspace）
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>PlayerPrefs に保存するベストスコアのキー</summary>
    private const string BEST_SCORE_KEY = "MagnetGame_BestScore";

    // ────────────────────────────────────────────
    // Inspector で割り当てる参照
    // ────────────────────────────────────────────

    [Header("UI Reference")]
    [Tooltip("HUD（スコア・タイマー等）を表示する UIController")]
    public UIController uiController;

    [Tooltip("ゲーム終了時に表示するパネル GameObject")]
    public GameObject gameClearPanel;

    [Tooltip("GameClearPanel に付いている Controller。ランキング表示・名前入力を担う")]
    public GameClearPanelController gameClearPanelController;

    [Header("Game Settings")]
    [Tooltip("制限時間（秒）")]
    public float timeLimit = 60f;

    [Header("Scoring")]
    [Tooltip("Rare 的を 1 個ゴールさせたときの加点")]
    public int scorePerRare   = 30;
    [Tooltip("Normal 的を 1 個ゴールさせたときの加点")]
    public int scorePerNormal = 10;
    [Tooltip("Bad 的を 1 個ゴールさせたときの減点（正の値で記述、内部で減算する）")]
    public int scorePerBad    = 20;

    [Header("Spawner")]
    [Tooltip("的を動的生成する TargetSpawner")]
    public TargetSpawner spawner;

    // ────────────────────────────────────────────
    // 実行時状態（Inspector で読み取り専用表示）
    // ────────────────────────────────────────────

    [Header("Runtime State (read-only)")]
    [SerializeField, ReadOnly] private int   score;
    [SerializeField, ReadOnly] private int   rareGoalCount;
    [SerializeField, ReadOnly] private int   normalGoalCount;
    [SerializeField, ReadOnly] private int   badGoalCount;
    [SerializeField, ReadOnly] private bool  gameStarted;
    [SerializeField, ReadOnly] private bool  gameFinished;
    [SerializeField, ReadOnly] private float elapsedTime;
    [SerializeField, ReadOnly] private float remainingTime;
    [SerializeField, ReadOnly] private int   bestScore;

    // ────────────────────────────────────────────
    // 公開プロパティ（外部からは読み取りのみ）
    // ────────────────────────────────────────────

    public int   Score         => score;
    public bool  GameStarted   => gameStarted;
    public bool  GameFinished  => gameFinished;
    public float ElapsedTime   => elapsedTime;
    public float RemainingTime => remainingTime;
    public int   BestScore     => bestScore;
    public bool  IsPlaying     => gameStarted && !gameFinished;

    // ────────────────────────────────────────────
    // ライフサイクル
    // ────────────────────────────────────────────

    void Start()
    {
        // UI コントローラが未割り当てなら自動検索する（Inspector 結線忘れ対策）
        if (uiController == null)
        {
            uiController = FindFirstObjectByType<UIController>();
            if (uiController != null)
                Debug.LogWarning("[GameManager] uiController 未設定。自動検索で接続。");
        }

        // Spawner が未割り当てなら自動検索する
        if (spawner == null)
        {
            spawner = FindFirstObjectByType<TargetSpawner>();
            if (spawner != null)
                Debug.LogWarning("[GameManager] spawner 未設定。自動検索で接続。");
            else
                Debug.LogError("[GameManager] TargetSpawner が見つかりません。シーンに追加してください。");
        }

        ResetGame();
    }

    /// <summary>
    /// Idle 状態に戻す。スコア・カウンタ・時間を初期化し、
    /// GameClearPanel を隠し、UI を更新する。
    /// </summary>
    public void ResetGame()
    {
        score           = 0;
        normalGoalCount = 0;
        rareGoalCount   = 0;
        badGoalCount    = 0;
        gameStarted     = false;
        gameFinished    = false;
        elapsedTime     = 0f;
        remainingTime   = timeLimit;

        // 既存のベストスコアを PlayerPrefs から読み込む
        bestScore = PlayerPrefs.GetInt(BEST_SCORE_KEY, 0);

        if (gameClearPanel != null) gameClearPanel.SetActive(false);
        UpdateUI();
    }

    // ────────────────────────────────────────────
    // 毎フレームの状態遷移
    // ────────────────────────────────────────────

    void Update()
    {
        // ── Idle 状態 ── Space キーでゲーム開始
        if (!gameStarted && !gameFinished)
        {
            if (Input.GetKeyDown(KeyCode.Space))
                StartGame();
            return;
        }

        // ── Playing 状態 ── 時間更新と制限時間切れ判定
        if (gameStarted && !gameFinished)
        {
            elapsedTime  += Time.deltaTime;
            remainingTime = Mathf.Max(0f, timeLimit - elapsedTime);

            if (uiController != null)
            {
                uiController.SetTime(elapsedTime);
                uiController.SetRemainingTime(remainingTime);
            }

            if (remainingTime <= 0f) FinishGame();
        }

        // 名前入力中はキー入力をショートカットとして扱わない（"r" を含む名前でリスタートしてしまうのを防ぐ）
        if (IsTypingText()) return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // ── Finished 状態は R、中断要件対応として Shift+R ならいつでもリスタート
        if (Input.GetKeyDown(KeyCode.R) && (gameFinished || shift))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // Shift+Backspace でベスト記録とランキングを全リセット
        if (shift && Input.GetKeyDown(KeyCode.Backspace))
        {
            PlayerPrefs.DeleteKey(BEST_SCORE_KEY);
            PlayerPrefs.Save();
            bestScore = 0;
            Ranking.Clear();
            UpdateUI();
            Debug.Log("[GameManager] ベスト記録とランキングをリセットしました。");
        }
    }

    /// <summary>InputField にフォーカスがあり、文字入力中かどうか</summary>
    private static bool IsTypingText()
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null) return false;
        var field = selected.GetComponent<InputField>();
        return field != null && field.isFocused;
    }

    // ────────────────────────────────────────────
    // 状態遷移メソッド
    // ────────────────────────────────────────────

    /// <summary>Idle → Playing に遷移する</summary>
    void StartGame()
    {
        gameStarted   = true;
        elapsedTime   = 0f;
        remainingTime = timeLimit;
        Debug.Log($"[GameManager] SCORE ATTACK START - Time Limit: {timeLimit}s");

        // Spawner に的の動的生成を開始させる
        if (spawner != null)
            spawner.BeginSpawning();
        else
            Debug.LogError("[GameManager] Spawner が null です。動的生成が起こりません。");

        UpdateUI();
    }

    /// <summary>
    /// TargetObject がゴールに到達したときに呼ばれる。
    /// 的のタイプに応じてスコアを加減算し、新しい的を 1 個生成する。
    /// </summary>
    public void OnTargetReachedGoal(TargetType type)
    {
        // ゲーム終了後・開始前のゴールは無視
        if (!IsPlaying) return;

        // 的のタイプに応じてスコアを加減算する（Bad は減点）
        switch (type)
        {
            case TargetType.Normal:
                score += scorePerNormal;
                normalGoalCount++;
                break;
            case TargetType.Rare:
                score += scorePerRare;
                rareGoalCount++;
                break;
            case TargetType.Bad:
                score -= scorePerBad;
                badGoalCount++;
                break;
            default:
                Debug.LogWarning($"[GameManager] Unknown TargetType: {type}");
                break;
        }
        UpdateUI();

        // 的が 1 個ゴールしたので、補充として 1 個新しく生成する
        if (spawner != null)
            spawner.RequestSpawn();
    }

    /// <summary>HUD（スコア・時間・状態テキスト）を最新に更新する</summary>
    void UpdateUI()
    {
        if (uiController == null) return;
        uiController.SetScore(score);
        uiController.SetTime(elapsedTime);
        uiController.SetRemainingTime(remainingTime);
        uiController.SetBestScore(bestScore);
        uiController.SetGameState(gameStarted, gameFinished);
    }

    /// <summary>Playing → Finished に遷移する</summary>
    void FinishGame()
    {
        gameFinished = true;
        Debug.Log(
            $"[GameManager] GAME OVER - Score: {score} " +
            $"(Normal: {normalGoalCount}, Rare: {rareGoalCount}, Bad: {badGoalCount})"
        );

        // Spawner に的の生成を止めさせる
        if (spawner != null)
            spawner.StopSpawning();

        // ベストスコアの更新判定
        bool isNewBest = score > bestScore;
        if (isNewBest)
        {
            bestScore = score;
            PlayerPrefs.SetInt(BEST_SCORE_KEY, bestScore);
            PlayerPrefs.Save();
            Debug.Log($"[GameManager] NEW BEST SCORE: {bestScore}");
        }

        // ゲームクリアパネルを表示
        if (gameClearPanel != null)
            gameClearPanel.SetActive(true);

        // GameClearPanelController があればそちらにスコアとカウンタを渡して表示させる。
        if (gameClearPanelController != null)
        {
            gameClearPanelController.Show(score, normalGoalCount, rareGoalCount, badGoalCount);
        }

        UpdateUI();
    }
}
