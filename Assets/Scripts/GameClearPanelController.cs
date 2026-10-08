using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ゲームクリア時（FinishGame）に表示されるパネルの中身を制御する。
///
/// 担当：
///   - Final Score 表示
///   - 名前入力欄（InputField）
///   - 名前確定ボタン
///   - ランキング TOP 5 表示
///   - Press R to Restart 表記
///
/// GameManager から Show(finalScore, normal, rare, bad) で呼ばれる。
/// </summary>
public class GameClearPanelController : MonoBehaviour
{
    [Header("UI Refs")]
    [Tooltip("Final Score 表示テキスト")]
    public Text finalScoreText;

    [Tooltip("名前入力欄（InputField）。null なら名前入力 UI なし＝匿名扱い")]
    public InputField nameInputField;

    [Tooltip("名前確定ボタン。null なら InputField の EndEdit でも確定")]
    public Button submitButton;

    [Header("Ranking columns (4-column display, optional)")]
    [Tooltip("順位列の Text。設定するとこちらが優先される")]
    public Text rankingRanksText;
    [Tooltip("名前列の Text")]
    public Text rankingNamesText;
    [Tooltip("スコア列の Text")]
    public Text rankingScoresText;
    [Tooltip("日付列の Text")]
    public Text rankingDatesText;

    [Tooltip("ランクイン演出用テキスト（NEW BEST / RANK IN! など）")]
    public Text newRecordText;

    [Header("Behavior")]
    [Tooltip("名前未入力時のデフォルト")]
    public string defaultName = "Anonymous";

    [Tooltip("名前の最大文字数")]
    public int nameMaxLength = 10;

    // 内部状態
    private int currentScore;
    private bool submitted;

    /// <summary>
    /// Awake で名前入力欄の文字数制限をセットし、ボタンのクリックイベントにリスナーを追加する。
    /// </summary>
    void Awake()
    {
        if (nameInputField != null)
        {
            nameInputField.characterLimit = nameMaxLength;
            nameInputField.onEndEdit.AddListener(OnInputEndEdit);
        }
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(SubmitName);
        }
    }

    /// <summary>
    /// GameManager.FinishGame から呼ばれる。
    /// パネル本体の SetActive は呼び元の責任。
    /// </summary>
    public void Show(int finalScore, int normalCount, int rareCount, int badCount)
    {
        currentScore = finalScore;
        submitted = false;

        if (finalScoreText != null)
        {
            finalScoreText.text =
                $"FINAL SCORE: {finalScore}\n" +
                $"Normal: {normalCount}   Rare: {rareCount}   Bad: {badCount}";
        }

        bool willRankIn = Ranking.IsRankIn(finalScore);
        if (newRecordText != null)
        {
            newRecordText.text = willRankIn ? "★ RANK IN! 名前を入力してください ★" : "";
        }

        if (nameInputField != null)
        {
            nameInputField.text = "";
            nameInputField.interactable = willRankIn;
            if (willRankIn) nameInputField.Select();
        }
        if (submitButton != null)
        {
            submitButton.interactable = willRankIn;
        }

        // 先に現在のランキングを表示（未確定時点）
        RefreshRanking();
    }

    /// <summary>
    /// 名前入力欄の EndEdit イベントから呼ばれる。
    /// EndEdit はフォーカスが外れたときにも呼ばれるため、Enter で確定された場合のみ送信する。
    /// </summary>
    private void OnInputEndEdit(string text)
    {
        if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))
        {
            SubmitName();
        }
    }

    /// <summary>
    /// 名前をランキングに送信する。すでに送信済みなら何もしない。
    /// ランキングに送信した後は、名前入力 UI を操作不可にして複数回送信できないようにする。
    /// ランクインしたら newRecordText にランクインしたことを表示する。ランク外ならそのまま。
    /// </summary>
    public void SubmitName()
    {
        if (submitted) return;

        // 名前が未入力・空白のみなら defaultName（匿名扱い）
        string name = (nameInputField != null) ? nameInputField.text : defaultName;
        if (string.IsNullOrWhiteSpace(name)) name = defaultName;

        int rank = Ranking.Submit(name, currentScore);  // ランクインなら 1 以上、ランク外なら -1
        submitted = true;

        // 複数回送信できないように入力 UI を無効化
        if (nameInputField != null) nameInputField.interactable = false;
        if (submitButton != null)   submitButton.interactable = false;

        if (newRecordText != null)
        {
            newRecordText.text = (rank > 0)
                ? $"★ {rank} 位にランクイン！ ★"
                : "ランク外でした";
        }

        RefreshRanking();
        Debug.Log($"[GameClearPanelController] Submitted: {name} {currentScore} -> rank {rank}");
    }

    // 4 列の Text それぞれにランキングを流し込む（未割り当ての列はスキップ）
    private void RefreshRanking()
    {
        var list = Ranking.Load();
        if (rankingRanksText != null)  rankingRanksText.text  = Ranking.FormatRanks(list);
        if (rankingNamesText != null)  rankingNamesText.text  = Ranking.FormatNames(list);
        if (rankingScoresText != null) rankingScoresText.text = Ranking.FormatScores(list);
        if (rankingDatesText != null)  rankingDatesText.text  = Ranking.FormatDates(list);
    }
}
