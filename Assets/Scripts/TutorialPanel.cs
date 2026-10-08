using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ゲーム開始前（Idle 状態）に操作説明をオーバーレイ表示するパネル。
///
/// 仕様：
///   - Awake 時点で表示 ON
///   - UIController.SetGameState(started, finished) から
///     ApplyGameState(started, finished) が呼ばれることで表示切替
///     started=true（Playing）       → フェードアウトして非表示
///     finished=true（GameOver）     → 非表示のまま
///     started=false & finished=false（Idle/リスタート直後）→ 再表示
///
/// アタッチ先：Canvas 配下の専用 Panel GameObject（Image + 子 Text 群）
/// CanvasGroup を持っていればフェードに使う。なければ自動追加。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TutorialPanel : MonoBehaviour
{
    [Header("Fade")]
    [Tooltip("フェード時間（秒）。0 なら即時切替")]
    public float fadeDuration = 0.4f;

    [Tooltip("CanvasGroup（未割り当てなら自動取得・自動追加）")]
    public CanvasGroup canvasGroup;

    [Header("Content Texts (Optional)")]
    [Tooltip("見出しテキスト")]
    public Text titleText;
    [Tooltip("操作説明本文")]
    public Text bodyText;
    [Tooltip("最下段の促し")]
    public Text footerText;

    // titleText / bodyText / footerText が空のときに表示するデフォルトテキスト
    [Header("Default Content")]
    [TextArea(3, 10)]
    public string defaultBody =
        "● 右手：位置を動かす + 「グー」で磁石ON（パーで磁石OFF）\n" +
        "● 左手：開閉で磁力の強さを調整\n" +
        "    パー = 弱い・狭い／グー = 強い・広い\n" +
        "● 制限時間 60 秒で高スコアを目指してください\n" +
        "\n" +
        "ターゲット\n" +
        "    銀（Normal）= +10 / 金（Rare）= +30 / 赤（Bad）= -20\n" +
        "右側の土管に的を運び入れて得点してください\n";

    public string defaultTitle = "HOW TO PLAY";
    public string defaultFooter = "Press SPACE to Start";

    // 目標アルファ値。Update で canvasGroup.alpha をこの値に向けてフェードさせる
    private float targetAlpha = 1f;

    // 保険：UIController から呼ばれなくても GameManager 状態を見て表示を切り替える
    private GameManager cachedGm;

    // 起動時に CanvasGroup を確保し、テキストが空ならデフォルトをセットして、表示状態を初期化
    void Awake()
    {
        EnsureCanvasGroup();
        cachedGm = FindFirstObjectByType<GameManager>();

        if (titleText != null && string.IsNullOrEmpty(titleText.text)) titleText.text = defaultTitle;
        if (bodyText != null && string.IsNullOrEmpty(bodyText.text)) bodyText.text = defaultBody;
        if (footerText != null && string.IsNullOrEmpty(footerText.text)) footerText.text = defaultFooter;

        // 起動時は確実に表示
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = false; // ゲーム入力を阻害しない
        canvasGroup.interactable = false;
        targetAlpha = 1f;
    }

    // 毎フレーム、GameManager の状態を見て表示すべきか判断し、必要ならフェードさせる
    void Update()
    {
        // UIController が tutorialPanel を結線していない場合でも動くように直接ポーリングする
        if (cachedGm != null)
            ApplyGameState(cachedGm.GameStarted, cachedGm.GameFinished);

        if (Mathf.Approximately(canvasGroup.alpha, targetAlpha)) return;

        if (fadeDuration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            return;
        }

        // unscaledDeltaTime を使い、timeScale = 0 の一時停止中でもフェードを完了させる
        float step = Time.unscaledDeltaTime / fadeDuration;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, step);
    }

    /// <summary>
    /// UIController.SetGameState から呼ばれる。
    /// Idle 中だけ表示、Playing / Finished では非表示。
    /// </summary>
    public void ApplyGameState(bool started, bool finished)
    {
        bool shouldShow = (!started && !finished);
        targetAlpha = shouldShow ? 1f : 0f;
    }

    /// <summary>外部から手動で表示切替したい場合用</summary>
    public void Show() { targetAlpha = 1f; }
    public void Hide() { targetAlpha = 0f; }

    // 内部：CanvasGroup を確保
    private void EnsureCanvasGroup()
    {
        // Inspector 割り当て → 自身の GetComponent → AddComponent の順で確保
        if (canvasGroup != null) return;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
}
