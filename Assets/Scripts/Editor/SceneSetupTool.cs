#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

/// <summary>
/// シーン構築の手作業を最小化する Editor 補助。
/// Tools → Magnet → Setup Everything 一発で全部組み上がる。
/// </summary>
public static class SceneSetupTool
{
    private const string ROOT_TARGETS = "Targets";
    private const string GOAL_NAME = "Goal";
    private const string FIELD_LINE_NAME = "MagneticFieldLine";
    private const string CANVAS_NAME = "Canvas";
    private const string GM_NAME = "GameManager";
    private const string SPAWNER = "TargetSpawner";

    private const string LEAP_PROVIDER = "LeapServiceProvider";
    private const string LEAP_RIGHT = "RightHandController";
    private const string LEAP_LEFT = "LeftHandController";
    private const string LEAP_INPUT = "LeapHandInput";
    private const string SIM_INPUT = "SimulatedHandInput";
    private const string SWITCHER = "InputModeSwitcher";
    private const string TUTORIAL_PANEL = "TutorialPanel";
    private const string GAMECLEAR_PANEL = "GameClearPanel";
    private const string BOUNDARY_WALLS = "BoundaryWalls";
    private const string ATMOSPHERE = "MagnetAtmosphere";
    private const string MAGNET_VISUAL = "MagnetVisual";
    private const string OBSTACLES_ROOT = "Obstacles";

    // =========================================================
    // メインメニュー
    // =========================================================

    [MenuItem("Tools/Magnet/Setup Everything (Recommended)")]
    public static void SetupEverything()
    {
        EnsureTag("Goal");
        EnsureTag("Target");

        SetupCamera();
        var targetsRoot = GenerateTargets();
        GenerateGoal();
        var fieldLineGO = GenerateFieldLine();
        var canvasGO = GenerateCanvasUI();
        GenerateTutorialPanel(canvasGO);
        var simInputGO = GenerateSimulatedHandInput();
        var leapInputGO = TryGenerateLeapHandInput();
        var switcherGO = GenerateInputModeSwitcher(simInputGO, leapInputGO);
        var spawnerGO = GenerateSpawner(targetsRoot != null ? targetsRoot.transform : null);
        var gmGO = GenerateGameManager(canvasGO, spawnerGO);
        var gameClearGO = GenerateGameClearPanel(canvasGO);
        WireGameClearPanelToManager(gmGO, gameClearGO);

        GenerateBoundaryWalls();
        GenerateMagnetVisual(simInputGO);

        WireUpReferences(fieldLineGO, canvasGO, switcherGO, simInputGO, leapInputGO, gmGO);

        Debug.Log("[SceneSetupTool] Setup Everything 完了。Play で開始（Space キーでスタート）。");
        if (leapInputGO == null)
            Debug.LogWarning("[SceneSetupTool] Leap 関連 GameObject は生成されませんでした（SDK 未検出）。");
    }

    // =========================================================
    // 個別メニュー
    // =========================================================

    [MenuItem("Tools/Magnet/Individual/Setup Camera (top-down XY)")]
    public static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            cam = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();
        }
        // 少し斜めにして Z 軸の高さが見えるようにする（Perspective）
        cam.transform.position = new Vector3(0f, -3f, -10f);
        cam.transform.rotation = Quaternion.Euler(-15f, 0f, 0f);
        cam.orthographic = false;
        // FOV を狭く（望遠寄り）：的を大きく見せ、フィールド全体を画面に収める
        // 35 = 望遠寄り（推奨）、25 = さらにズーム、50 = 元の広角
        cam.fieldOfView = 35f;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
        Debug.Log("[SceneSetupTool] カメラを FOV=35 で配置しました。");
    }

    [MenuItem("Tools/Magnet/Individual/Generate Targets (initial spheres)")]
    public static GameObject GenerateTargets()
    {
        var existing = GameObject.Find(ROOT_TARGETS);
        if (existing != null) Object.DestroyImmediate(existing);

        var root = new GameObject(ROOT_TARGETS);
        Undo.RegisterCreatedObjectUndo(root, "Generate Targets");

        Material redMat = LoadOrCreateMaterial("Assets/Materials/TargetMaterial.mat", new Color(0.9f, 0.15f, 0.15f));
        Material blueMat = LoadOrCreateMaterial("Assets/Materials/BadTargetMaterial.mat", new Color(0.2f, 0.4f, 0.95f));
        Random.InitState(12345);

        // Idle 中の見栄え用に少しだけ配置（ゲーム開始で Spawner が全クリアして配置し直す）
        for (int i = 0; i < 6; i++)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"Target_{i:00}";
            if (TagExists("Target")) sphere.tag = "Target";
            sphere.transform.SetParent(root.transform);
            sphere.transform.position = new Vector3(
                Random.Range(-4f, 4f),
                Random.Range(-4f, 4f),
                Random.Range(-0.8f, 0.8f)
            );
            sphere.transform.localScale = Vector3.one * 0.4f;

            var rb = sphere.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.drag = 1.5f;
            rb.angularDrag = 2.0f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;

            var to = sphere.AddComponent<TargetObject>();
            // 初期配置は全部 Normal（Spawner が走ると入れ替わる）
            to.type = TargetType.Normal;
            sphere.GetComponent<MeshRenderer>().sharedMaterial = redMat;
        }

        Selection.activeObject = root;
        Debug.Log("[SceneSetupTool] Target 初期 6 個を生成しました（ゲーム開始で Spawner が再配置）。");
        return root;
    }

    [MenuItem("Tools/Magnet/Individual/Generate Goal Cube")]
    public static GameObject GenerateGoal()
    {
        var existing = GameObject.Find(GOAL_NAME);
        if (existing != null) Object.DestroyImmediate(existing);

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = GOAL_NAME;
        cube.transform.position = new Vector3(4f, 4f, 0f);
        cube.transform.localScale = Vector3.one * 1.0f;
        if (TagExists("Goal")) cube.tag = "Goal";
        cube.GetComponent<BoxCollider>().isTrigger = true;

        // 発光する立方体（Emission を有効にしたマテリアル）
        Material glowMat = LoadOrCreateEmissiveMaterial("Assets/Materials/GoalMaterial.mat",
            new Color(0.15f, 0.8f, 0.2f), new Color(0.2f, 1.5f, 0.4f));
        cube.GetComponent<MeshRenderer>().sharedMaterial = glowMat;

        Undo.RegisterCreatedObjectUndo(cube, "Generate Goal");
        Debug.Log("[SceneSetupTool] 発光する Goal を (4, 4, 0) に配置しました。");
        return cube;
    }

    /// <summary>
    /// Emission を有効にした Standard マテリアルを生成（または既存を流用）。
    /// </summary>
    private static Material LoadOrCreateEmissiveMaterial(string path, Color baseColor, Color emissionColor)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            EnsureFolder(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = baseColor;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emissionColor);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        AssetDatabase.SaveAssets();
        return mat;
    }

    [MenuItem("Tools/Magnet/Individual/Generate Field Line (LineRenderer)")]
    public static GameObject GenerateFieldLine()
    {
        var existing = GameObject.Find(FIELD_LINE_NAME);
        if (existing != null) Object.DestroyImmediate(existing);

        // 親オブジェクト：MagneticField 本体（自身にも XY リング用 LineRenderer を持たせる）
        var go = new GameObject(FIELD_LINE_NAME);
        // XY 平面：local Z 軸を Z+（標準）に向ける
        var lrXY = CreateRingLineRenderer(go, "Ring_XY");

        // YZ 平面：local Z 軸を X 軸方向に向ける
        var childYZ = new GameObject("Ring_YZ");
        childYZ.transform.SetParent(go.transform, false);
        childYZ.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        var lrYZ = CreateRingLineRenderer(childYZ, "Ring_YZ");

        // XZ 平面：local Z 軸を Y 軸方向に向ける
        var childXZ = new GameObject("Ring_XZ");
        childXZ.transform.SetParent(go.transform, false);
        childXZ.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var lrXZ = CreateRingLineRenderer(childXZ, "Ring_XZ");

        var mf = go.AddComponent<MagneticField>();
        mf.fieldLineXY = lrXY;
        mf.fieldLineYZ = lrYZ;
        mf.fieldLineXZ = lrXZ;

        Undo.RegisterCreatedObjectUndo(go, "Generate FieldLine (Sphere)");
        return go;
    }

    /// <summary>
    /// 球状可視化用の 1 本分の LineRenderer を生成する。
    /// </summary>
    private static LineRenderer CreateRingLineRenderer(GameObject host, string label)
    {
        var lr = host.GetComponent<LineRenderer>();
        if (lr == null) lr = host.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.startWidth = 0.06f;
        lr.endWidth = 0.06f;
        lr.material = LoadOrCreateLineMaterial("Assets/Materials/LineRendererMaterial.mat");
        lr.startColor = Color.yellow;
        lr.endColor = Color.yellow;
        lr.positionCount = 0;
        lr.alignment = LineAlignment.TransformZ; // 各リングの軸を固定し、球らしく交差させる
        return lr;
    }

    [MenuItem("Tools/Magnet/Individual/Generate Canvas UI")]
    public static GameObject GenerateCanvasUI()
    {
        var existing = GameObject.Find(CANVAS_NAME);
        if (existing != null && existing.GetComponent<Canvas>() != null) Object.DestroyImmediate(existing);

        var canvasGO = new GameObject(CANVAS_NAME);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        if (GameObject.Find("EventSystem") == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // 左上
        Text scoreText = CreateUIText(canvasGO.transform, "ScoreText", "Score: 0",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), TextAnchor.UpperLeft, 28);
        Text bestScoreText = CreateUIText(canvasGO.transform, "BestScoreText", "Best Score: 0",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -55), TextAnchor.UpperLeft, 18);

        // 中央上
        Text rangeText = CreateUIText(canvasGO.transform, "RangeText", "Range: --",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-100, -55), TextAnchor.UpperCenter, 18);
        Text forceText = CreateUIText(canvasGO.transform, "ForceText", "Force: --",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(100, -55), TextAnchor.UpperCenter, 18);

        // 右上
        Text remainingTimeText = CreateUIText(canvasGO.transform, "RemainingTimeText", "Remain: 60.0s",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), TextAnchor.UpperRight, 26);
        Text timeText = CreateUIText(canvasGO.transform, "TimeText", "Time: 0.00s",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -55), TextAnchor.UpperRight, 18);
        Text targetText = CreateUIText(canvasGO.transform, "TargetCountText", "",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -80), TextAnchor.UpperRight, 18);
        Text bestText = CreateUIText(canvasGO.transform, "BestTimeText", "Best: --",
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -105), TextAnchor.UpperRight, 18);

        // 中央
        Text pressSpaceText = CreateUIText(canvasGO.transform, "PressSpaceText", "Press SPACE to Start",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), TextAnchor.MiddleCenter, 36);
        Text gameOverText = CreateUIText(canvasGO.transform, "GameOverText", "",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), TextAnchor.MiddleCenter, 28);

        // 下
        Text opennessText = CreateUIText(canvasGO.transform, "OpennessText", "Openness: 1.00",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), TextAnchor.LowerCenter, 22);
        Text rHandText = CreateUIText(canvasGO.transform, "RightHandPositionText", "R-Hand: --",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), TextAnchor.LowerLeft, 18);

        var ui = canvasGO.AddComponent<UIController>();
        ui.scoreText = scoreText;
        ui.bestScoreText = bestScoreText;
        ui.rangeText = rangeText;
        ui.forceText = forceText;
        ui.timeText = timeText;
        ui.remainingTimeText = remainingTimeText;
        ui.opennessText = opennessText;
        ui.rightHandPositionText = rHandText;
        ui.pressSpaceText = pressSpaceText;
        ui.gameOverText = gameOverText;

        Undo.RegisterCreatedObjectUndo(canvasGO, "Generate Canvas UI");
        return canvasGO;
    }

    [MenuItem("Tools/Magnet/Individual/Generate Tutorial Panel")]
    public static GameObject GenerateTutorialPanelMenu()
    {
        var canvas = GameObject.Find(CANVAS_NAME);
        if (canvas == null)
        {
            Debug.LogError("[SceneSetupTool] Canvas が見つかりません。先に Generate Canvas UI を実行してください。");
            return null;
        }
        return GenerateTutorialPanel(canvas);
    }

    /// <summary>
    /// Canvas 配下に Idle 中表示する操作説明パネルを生成し、UIController と結線する。
    /// 既存の TutorialPanel があれば作り直す。
    /// </summary>
    private static GameObject GenerateTutorialPanel(GameObject canvasGO)
    {
        if (canvasGO == null) return null;

        // 既存削除
        var existing = canvasGO.transform.Find(TUTORIAL_PANEL);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // Panel 本体（半透明黒の背景）
        var panel = new GameObject(TUTORIAL_PANEL);
        panel.transform.SetParent(canvasGO.transform, false);
        var rt = panel.AddComponent<RectTransform>();
        // 画面中央 60% を覆う矩形
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(720, 460);
        rt.anchoredPosition = new Vector2(0, 40);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        bg.raycastTarget = false;

        var cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // 子テキスト：タイトル
        var titleText = CreateUIText(panel.transform, "TitleText", "HOW TO PLAY",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30),
            TextAnchor.UpperCenter, 32);
        var titleRT = titleText.GetComponent<RectTransform>();
        titleRT.sizeDelta = new Vector2(680, 50);

        // 子テキスト：本文（左揃え）
        var bodyText = CreateUIText(panel.transform, "BodyText",
            "● 右手：位置を動かす + 「グー」で磁石ON（パーでOFF）\n" +
            "● 左手：開閉で磁力の強さを調整\n" +
            "    パー = 弱い・狭い／グー = 強い・広い\n" +
            "● 制限時間 60 秒で高スコアを目指してください\n\n" +
            "ターゲット\n" +
            "    赤（Normal）= +10／金（Rare）= +30／青（Bad）= -20\n" +
            "右側の土管に的を運び入れて得点してください\n" +
            "（Sim モード：マウス位置＋左クリックで磁石ON）",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10),
            TextAnchor.MiddleLeft, 20);
        var bodyRT = bodyText.GetComponent<RectTransform>();
        bodyRT.sizeDelta = new Vector2(640, 320);

        // 子テキスト：フッター
        var footerText = CreateUIText(panel.transform, "FooterText", "Press SPACE to Start",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30),
            TextAnchor.LowerCenter, 22);
        var footerRT = footerText.GetComponent<RectTransform>();
        footerRT.sizeDelta = new Vector2(680, 40);

        // TutorialPanel スクリプト
        var tp = panel.AddComponent<TutorialPanel>();
        tp.canvasGroup = cg;
        tp.titleText = titleText;
        tp.bodyText = bodyText;
        tp.footerText = footerText;

        // UIController と結線
        var ui = canvasGO.GetComponent<UIController>();
        if (ui != null) ui.tutorialPanel = tp;

        Undo.RegisterCreatedObjectUndo(panel, "Generate Tutorial Panel");
        Debug.Log("[SceneSetupTool] TutorialPanel を Canvas 配下に生成し UIController と結線しました。");
        return panel;
    }

    [MenuItem("Tools/Magnet/Individual/Generate GameClear Panel")]
    public static GameObject GenerateGameClearPanelMenu()
    {
        var canvas = GameObject.Find(CANVAS_NAME);
        if (canvas == null)
        {
            Debug.LogError("[SceneSetupTool] Canvas が見つかりません。先に Generate Canvas UI を実行してください。");
            return null;
        }
        var go = GenerateGameClearPanel(canvas);
        var gm = GameObject.Find(GM_NAME);
        WireGameClearPanelToManager(gm, go);
        return go;
    }

    /// <summary>
    /// Canvas 配下に GameClearPanel を生成し GameClearPanelController を結線する。
    /// 既存の GameClearPanel があれば作り直す。
    /// </summary>
    private static GameObject GenerateGameClearPanel(GameObject canvasGO)
    {
        if (canvasGO == null) return null;

        // 既存削除
        var existing = canvasGO.transform.Find(GAMECLEAR_PANEL);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // Panel 本体
        var panel = new GameObject(GAMECLEAR_PANEL);
        panel.transform.SetParent(canvasGO.transform, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(780, 580);
        rt.anchoredPosition = Vector2.zero;

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.88f);
        bg.raycastTarget = true;

        // FinalScoreText（上部）
        var finalScore = CreateUIText(panel.transform, "FinalScoreText", "FINAL SCORE: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40),
            TextAnchor.UpperCenter, 30);
        finalScore.GetComponent<RectTransform>().sizeDelta = new Vector2(740, 90);

        // NewRecordText（FinalScore の少し下、強調）
        var newRec = CreateUIText(panel.transform, "NewRecordText", "",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -140),
            TextAnchor.UpperCenter, 22);
        newRec.GetComponent<RectTransform>().sizeDelta = new Vector2(740, 36);
        newRec.color = new Color(1f, 0.85f, 0.2f);

        // 名前入力 (InputField)
        var inputGO = new GameObject("NameInputField");
        inputGO.transform.SetParent(panel.transform, false);
        var inputRT = inputGO.AddComponent<RectTransform>();
        inputRT.anchorMin = new Vector2(0.5f, 1f);
        inputRT.anchorMax = new Vector2(0.5f, 1f);
        inputRT.pivot = new Vector2(0.5f, 1f);
        inputRT.sizeDelta = new Vector2(280, 40);
        inputRT.anchoredPosition = new Vector2(-100, -190);

        var inputImg = inputGO.AddComponent<Image>();
        inputImg.color = new Color(1f, 1f, 1f, 0.9f);

        var inputField = inputGO.AddComponent<InputField>();
        inputField.characterLimit = 10;

        // InputField の Placeholder と Text を子として作る
        var ph = CreateUIText(inputGO.transform, "Placeholder", "名前を入力 (最大10文字)",
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero,
            TextAnchor.MiddleLeft, 18);
        var phRT = ph.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(10, 2); phRT.offsetMax = new Vector2(-10, -2);
        ph.color = new Color(0.4f, 0.4f, 0.4f);
        ph.fontStyle = FontStyle.Italic;

        var txt = CreateUIText(inputGO.transform, "Text", "",
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero,
            TextAnchor.MiddleLeft, 18);
        var txtRT = txt.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(10, 2); txtRT.offsetMax = new Vector2(-10, -2);
        txt.color = Color.black;
        txt.supportRichText = false;

        inputField.placeholder = ph;
        inputField.textComponent = txt;

        // 名前確定ボタン
        var btnGO = new GameObject("SubmitButton");
        btnGO.transform.SetParent(panel.transform, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 1f);
        btnRT.anchorMax = new Vector2(0.5f, 1f);
        btnRT.pivot = new Vector2(0.5f, 1f);
        btnRT.sizeDelta = new Vector2(120, 40);
        btnRT.anchoredPosition = new Vector2(120, -190);

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.3f, 0.7f, 0.3f);

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        var btnLabel = CreateUIText(btnGO.transform, "Label", "登録",
            new Vector2(0, 0), new Vector2(1, 1), Vector2.zero,
            TextAnchor.MiddleCenter, 20);
        var blRT = btnLabel.GetComponent<RectTransform>();
        blRT.anchorMin = Vector2.zero; blRT.anchorMax = Vector2.one;
        blRT.offsetMin = Vector2.zero; blRT.offsetMax = Vector2.zero;
        btnLabel.color = Color.white;

        // RankingHeader
        var rankHeader = CreateUIText(panel.transform, "RankingHeader", "── TOP 5 ──",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30),
            TextAnchor.MiddleCenter, 22);
        rankHeader.GetComponent<RectTransform>().sizeDelta = new Vector2(720, 32);
        rankHeader.color = new Color(0.9f, 0.9f, 0.4f);

        // Press R to Restart（最下段）
        var restart = CreateUIText(panel.transform, "RestartHintText", "Press R to Restart  /  Shift+R で強制リセット",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 24),
            TextAnchor.LowerCenter, 18);
        restart.GetComponent<RectTransform>().sizeDelta = new Vector2(740, 28);

        // Controller アタッチ
        var ctrl = panel.AddComponent<GameClearPanelController>();
        ctrl.finalScoreText = finalScore;
        ctrl.nameInputField = inputField;
        ctrl.submitButton = btn;
        ctrl.newRecordText = newRec;

        // 起動時は非表示
        panel.SetActive(false);

        Undo.RegisterCreatedObjectUndo(panel, "Generate GameClear Panel");
        Debug.Log("[SceneSetupTool] GameClearPanel を生成しました。");
        return panel;
    }

    private static void WireGameClearPanelToManager(GameObject gmGO, GameObject panelGO)
    {
        if (gmGO == null || panelGO == null) return;
        var gm = gmGO.GetComponent<GameManager>();
        if (gm == null) return;

        gm.gameClearPanel = panelGO;
        var ctrl = panelGO.GetComponent<GameClearPanelController>();
        if (ctrl != null)
        {
            gm.gameClearPanelController = ctrl;
        }
    }

    [MenuItem("Tools/Magnet/Individual/Setup Sim Input")]
    public static GameObject GenerateSimulatedHandInput()
    {
        var existing = GameObject.Find(SIM_INPUT);
        if (existing != null) Object.DestroyImmediate(existing);
        var go = new GameObject(SIM_INPUT);
        go.AddComponent<SimulatedHandInput>();
        Undo.RegisterCreatedObjectUndo(go, "Generate SimulatedHandInput");
        return go;
    }

    [MenuItem("Tools/Magnet/Individual/Setup Leap Input")]
    public static GameObject SetupLeapInputMenu()
    {
        var go = TryGenerateLeapHandInput();
        if (go == null)
        {
            EditorUtility.DisplayDialog("Ultraleap SDK が必要",
                "Leap.LeapServiceProvider 型が見つかりませんでした。",
                "OK");
        }
        return go;
    }

    private static GameObject TryGenerateLeapHandInput()
    {
        System.Type leapServiceProviderType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType("Leap.LeapServiceProvider", false)
                     ?? asm.GetType("Ultraleap.LeapServiceProvider", false);
                if (t != null) { leapServiceProviderType = t; break; }
            }
            catch { }
        }
        if (leapServiceProviderType == null) return null;

        foreach (var name in new[] { LEAP_PROVIDER, LEAP_RIGHT, LEAP_LEFT, LEAP_INPUT })
        {
            var existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);
        }

        var providerGO = new GameObject(LEAP_PROVIDER);
        var provider = providerGO.AddComponent(leapServiceProviderType);

        var rightGO = new GameObject(LEAP_RIGHT);
        var rightCtrl = rightGO.AddComponent<RightHandController>();
        SetLeapProviderField(rightCtrl, provider);

        var leftGO = new GameObject(LEAP_LEFT);
        var leftCtrl = leftGO.AddComponent<LeftHandController>();
        SetLeapProviderField(leftCtrl, provider);

        var leapInputGO = new GameObject(LEAP_INPUT);
        var leapInput = leapInputGO.AddComponent<LeapHandInput>();
        leapInput.rightHandController = rightCtrl;
        leapInput.leftHandController = leftCtrl;

        return leapInputGO;
    }

    private static void SetLeapProviderField(MonoBehaviour target, Component provider)
    {
        if (target == null || provider == null) return;
        var field = target.GetType().GetField("leapProvider", BindingFlags.Public | BindingFlags.Instance);
        if (field != null) field.SetValue(target, provider);
    }

    [MenuItem("Tools/Magnet/Individual/Setup InputModeSwitcher")]
    public static GameObject SetupSwitcherMenu()
    {
        var sim = GameObject.Find(SIM_INPUT);
        var leap = GameObject.Find(LEAP_INPUT);
        var simGO = sim != null ? sim : GenerateSimulatedHandInput();
        return GenerateInputModeSwitcher(simGO, leap);
    }

    private static GameObject GenerateInputModeSwitcher(GameObject simInputGO, GameObject leapInputGO)
    {
        var existing = GameObject.Find(SWITCHER);
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject(SWITCHER);
        var switcher = go.AddComponent<InputModeSwitcher>();
        if (simInputGO != null) switcher.simulatedHandInput = simInputGO.GetComponent<SimulatedHandInput>();
        if (leapInputGO != null) switcher.leapHandInput = leapInputGO.GetComponent<LeapHandInput>();

        var leapOnly = new System.Collections.Generic.List<GameObject>();
        foreach (var name in new[] { LEAP_PROVIDER, LEAP_RIGHT, LEAP_LEFT })
        {
            var g = GameObject.Find(name);
            if (g != null) leapOnly.Add(g);
        }
        switcher.leapOnlyObjects = leapOnly.ToArray();
        switcher.mode = InputModeSwitcher.Mode.Simulated;

        return go;
    }

    [MenuItem("Tools/Magnet/Individual/Generate Spawner")]
    public static GameObject SetupSpawnerMenu()
    {
        var root = GameObject.Find(ROOT_TARGETS);
        return GenerateSpawner(root != null ? root.transform : null);
    }

    private static GameObject GenerateSpawner(Transform targetsRoot)
    {
        var existing = GameObject.Find(SPAWNER);
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject(SPAWNER);
        var sp = go.AddComponent<TargetSpawner>();
        sp.targetsRoot = targetsRoot;

        sp.normalMaterial = LoadOrCreateMaterial("Assets/Materials/TargetMaterial.mat",
            new Color(0.9f, 0.15f, 0.15f));
        sp.badMaterial = LoadOrCreateMaterial("Assets/Materials/BadTargetMaterial.mat",
            new Color(0.2f, 0.4f, 0.95f));

        Undo.RegisterCreatedObjectUndo(go, "Generate Spawner");
        return go;
    }

    [MenuItem("Tools/Magnet/Individual/Generate GameManager")]
    public static GameObject SetupGameManagerMenu()
    {
        var canvas = GameObject.Find(CANVAS_NAME);
        var spawner = GameObject.Find(SPAWNER);
        return GenerateGameManager(canvas, spawner);
    }

    private static GameObject GenerateGameManager(GameObject canvasGO, GameObject spawnerGO)
    {
        var existing = GameObject.Find(GM_NAME);
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject(GM_NAME);
        var gm = go.AddComponent<GameManager>();
        if (canvasGO != null) gm.uiController = canvasGO.GetComponent<UIController>();
        if (spawnerGO != null) gm.spawner = spawnerGO.GetComponent<TargetSpawner>();
        Undo.RegisterCreatedObjectUndo(go, "Generate GameManager");
        return go;
    }

    private static void WireUpReferences(GameObject fieldLineGO, GameObject canvasGO,
        GameObject switcherGO, GameObject simInputGO, GameObject leapInputGO, GameObject gmGO)
    {
        var switcher = switcherGO != null ? switcherGO.GetComponent<InputModeSwitcher>() : null;
        if (switcher == null) return;

        var mf = fieldLineGO != null ? fieldLineGO.GetComponent<MagneticField>() : null;
        var ui = canvasGO != null ? canvasGO.GetComponent<UIController>() : null;
        switcher.magneticField = mf;
        switcher.uiController = ui;

        var simInput = simInputGO != null ? simInputGO.GetComponent<SimulatedHandInput>() : null;
        if (mf != null && simInput != null) mf.handInputSource = simInput;
        if (ui != null && simInput != null) ui.handInputSource = simInput;

        if (gmGO != null)
        {
            var gm = gmGO.GetComponent<GameManager>();
            if (gm != null && gm.uiController == null && ui != null) gm.uiController = ui;
        }
    }

    // =========================================================
    // 透明な壁 / 夜空ライティング / 磁石ビジュアル / 光の渦ゴール
    // =========================================================

    [MenuItem("Tools/Magnet/Individual/Generate Boundary Walls")]
    public static GameObject GenerateBoundaryWalls()
    {
        // 既存削除
        var existing = GameObject.Find(BOUNDARY_WALLS);
        if (existing != null) Object.DestroyImmediate(existing);

        var root = new GameObject(BOUNDARY_WALLS);

        // フィールドサイズ（TargetSpawner の xMin/xMax/yMin/yMax/zRange と整合）
        float xMin = -4.5f, xMax = 4.5f;
        float yMin = -4.5f, yMax = 4.5f;
        float zMin = -1.5f, zMax = 1.5f;
        float thickness = 0.5f;

        // 反発を少し付けた Physic Material
        var pm = LoadOrCreatePhysicMaterial("Assets/Materials/BoundaryPhysic.physicMaterial");

        // 6 面
        CreateWall(root.transform, "Wall_Right",   new Vector3(xMax + thickness * 0.5f, (yMin + yMax) * 0.5f, (zMin + zMax) * 0.5f), new Vector3(thickness, yMax - yMin + thickness, zMax - zMin + thickness), pm);
        CreateWall(root.transform, "Wall_Left",    new Vector3(xMin - thickness * 0.5f, (yMin + yMax) * 0.5f, (zMin + zMax) * 0.5f), new Vector3(thickness, yMax - yMin + thickness, zMax - zMin + thickness), pm);
        CreateWall(root.transform, "Wall_Top",     new Vector3((xMin + xMax) * 0.5f, yMax + thickness * 0.5f, (zMin + zMax) * 0.5f), new Vector3(xMax - xMin + thickness, thickness, zMax - zMin + thickness), pm);
        CreateWall(root.transform, "Wall_Bottom",  new Vector3((xMin + xMax) * 0.5f, yMin - thickness * 0.5f, (zMin + zMax) * 0.5f), new Vector3(xMax - xMin + thickness, thickness, zMax - zMin + thickness), pm);
        CreateWall(root.transform, "Wall_Back",    new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, zMax + thickness * 0.5f), new Vector3(xMax - xMin + thickness, yMax - yMin + thickness, thickness), pm);
        CreateWall(root.transform, "Wall_Front",   new Vector3((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, zMin - thickness * 0.5f), new Vector3(xMax - xMin + thickness, yMax - yMin + thickness, thickness), pm);

        Undo.RegisterCreatedObjectUndo(root, "Generate Boundary Walls");
        Debug.Log("[SceneSetupTool] BoundaryWalls 生成完了（6 面、見えない壁）。");
        return root;
    }

    private static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 size, PhysicMaterial pm)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.position = pos;
        cube.transform.localScale = size;

        // ガラスの壁：薄く透明＋わずかに発光（奥行きを把握しやすくする）
        var mr = cube.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.enabled = true;
            mr.sharedMaterial = LoadOrCreateGlassMaterial("Assets/Materials/GlassWall.mat");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        var col = cube.GetComponent<BoxCollider>();
        if (col != null && pm != null) col.material = pm;
    }

    /// <summary>
    /// ガラス風マテリアル（半透明・薄シアン・弱い発光）。Standard シェーダ Transparent モード。
    /// </summary>
    private static Material LoadOrCreateGlassMaterial(string path)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            EnsureFolder(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(mat, path);
        }
        // Standard シェーダを Transparent モードに（Rendering Mode = Transparent）
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;

        // 薄シアン + 透明
        mat.color = new Color(0.55f, 0.85f, 1.0f, 0.12f);
        // 弱い Emission で夜空でも縁が見える
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.10f, 0.25f, 0.35f));
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        // 反射を弱めて「ガラスっぽい」見た目に
        mat.SetFloat("_Glossiness", 0.85f);
        mat.SetFloat("_Metallic", 0.1f);

        AssetDatabase.SaveAssets();
        return mat;
    }

    private static PhysicMaterial LoadOrCreatePhysicMaterial(string path)
    {
        var pm = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
        if (pm == null)
        {
            pm = new PhysicMaterial("BoundaryPhysic");
            pm.bounciness = 0.3f;
            pm.dynamicFriction = 0.1f;
            pm.staticFriction = 0.1f;
            pm.bounceCombine = PhysicMaterialCombine.Maximum;
            pm.frictionCombine = PhysicMaterialCombine.Minimum;
            EnsureFolder(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(pm, path);
            AssetDatabase.SaveAssets();
        }
        return pm;
    }

    [MenuItem("Tools/Magnet/Individual/Generate Magnet Visual")]
    public static GameObject GenerateMagnetVisualMenu()
    {
        var sim = GameObject.Find(SIM_INPUT);
        return GenerateMagnetVisual(sim);
    }

    /// <summary>
    /// 右手位置に追従する U 字磁石の見た目を生成する。
    /// Simulated モードでは SimulatedHandInput の右手位置を、
    /// Leap モードでは Leap の手モデルが別途表示するため非表示にしておくと良い。
    /// </summary>
    public static GameObject GenerateMagnetVisual(GameObject simInputGO)
    {
        var existing = GameObject.Find(MAGNET_VISUAL);
        if (existing != null) Object.DestroyImmediate(existing);

        var root = new GameObject(MAGNET_VISUAL);

        // U 字磁石を組み立てる（円柱2本 + 連結部）
        var redMat = LoadOrCreateMaterial("Assets/Materials/MagnetRed.mat", new Color(0.85f, 0.15f, 0.15f));
        var grayMat = LoadOrCreateMaterial("Assets/Materials/MagnetGray.mat", new Color(0.7f, 0.7f, 0.75f));
        var whiteMat = LoadOrCreateMaterial("Assets/Materials/MagnetWhite.mat", new Color(0.95f, 0.95f, 0.95f));

        // 左極（N極：赤）
        var poleN = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        poleN.name = "PoleN";
        poleN.transform.SetParent(root.transform, false);
        poleN.transform.localPosition = new Vector3(-0.18f, 0.15f, 0f);
        poleN.transform.localScale = new Vector3(0.12f, 0.2f, 0.12f);
        Object.DestroyImmediate(poleN.GetComponent<Collider>());
        poleN.GetComponent<MeshRenderer>().sharedMaterial = redMat;

        // 右極（S極：白）
        var poleS = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        poleS.name = "PoleS";
        poleS.transform.SetParent(root.transform, false);
        poleS.transform.localPosition = new Vector3(0.18f, 0.15f, 0f);
        poleS.transform.localScale = new Vector3(0.12f, 0.2f, 0.12f);
        Object.DestroyImmediate(poleS.GetComponent<Collider>());
        poleS.GetComponent<MeshRenderer>().sharedMaterial = whiteMat;

        // 連結部（横棒）
        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "Bar";
        bar.transform.SetParent(root.transform, false);
        bar.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        bar.transform.localScale = new Vector3(0.48f, 0.1f, 0.12f);
        Object.DestroyImmediate(bar.GetComponent<Collider>());
        bar.GetComponent<MeshRenderer>().sharedMaterial = grayMat;

        // 左縦棒
        var leftArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftArm.name = "ArmLeft";
        leftArm.transform.SetParent(root.transform, false);
        leftArm.transform.localPosition = new Vector3(-0.18f, 0.025f, 0f);
        leftArm.transform.localScale = new Vector3(0.12f, 0.25f, 0.12f);
        Object.DestroyImmediate(leftArm.GetComponent<Collider>());
        leftArm.GetComponent<MeshRenderer>().sharedMaterial = grayMat;

        // 右縦棒
        var rightArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightArm.name = "ArmRight";
        rightArm.transform.SetParent(root.transform, false);
        rightArm.transform.localPosition = new Vector3(0.18f, 0.025f, 0f);
        rightArm.transform.localScale = new Vector3(0.12f, 0.25f, 0.12f);
        Object.DestroyImmediate(rightArm.GetComponent<Collider>());
        rightArm.GetComponent<MeshRenderer>().sharedMaterial = grayMat;

        // 追従スクリプトを追加
        var follower = root.AddComponent<MagnetVisualFollower>();
        if (simInputGO != null) follower.handInputSource = simInputGO.GetComponent<SimulatedHandInput>();

        Undo.RegisterCreatedObjectUndo(root, "Generate Magnet Visual");
        Debug.Log("[SceneSetupTool] MagnetVisual 生成完了。SimulatedHandInput の右手に追従します。");
        return root;
    }

    // =========================================================
    // 障害物（Obstacles）の自動配置メニュー
    // =========================================================

    [MenuItem("Tools/Magnet/Individual/Generate Obstacles")]
    public static GameObject GenerateObstacles()
    {
        // 既存削除
        var existing = GameObject.Find(OBSTACLES_ROOT);
        if (existing != null) Object.DestroyImmediate(existing);

        var root = new GameObject(OBSTACLES_ROOT);

        // 中央付近に板状の Cube を 6 個ランダム配置
        // 動線（左→右）を完全には塞がず、適度な迂回を要求する
        const int obstacleCount = 6;
        var mat = LoadOrCreateMaterial("Assets/Materials/ObstacleMaterial.mat",
            new Color(0.55f, 0.5f, 0.45f));  // 落ち着いた茶系グレー

        Random.InitState(42);  // 再現性のため固定シード
        for (int i = 0; i < obstacleCount; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Obstacle_{i:00}";
            cube.transform.SetParent(root.transform, false);

            // 中央付近（X: -1〜3、Y: -3〜3、Z: -0.3〜0.3）にランダム
            float x = Random.Range(-1f,  3f);
            float y = Random.Range(-3f,  3f);
            float z = Random.Range(-0.3f, 0.3f);
            cube.transform.position = new Vector3(x, y, z);

            // 板状のスケール（細長くしてランダム回転）
            float w = Random.Range(0.8f, 1.6f);
            float h = Random.Range(0.15f, 0.3f);
            float d = Random.Range(0.3f, 0.6f);
            cube.transform.localScale = new Vector3(w, h, d);

            // Z 軸まわりにランダム回転（板の傾きが画面で見やすい）
            float angle = Random.Range(-60f, 60f);
            cube.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // マテリアル
            var mr = cube.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;

            // Rigidbody は付けない（静的障害物）
            // BoxCollider は CreatePrimitive で自動付与済み、isTrigger = false（既定）のまま
            // タグは Untagged（Goal/Target ではない）
        }

        Undo.RegisterCreatedObjectUndo(root, "Generate Obstacles");
        Debug.Log($"[SceneSetupTool] 障害物 {obstacleCount} 個を生成しました。");
        return root;
    }

    // ===== ヘルパー =====

    private static Text CreateUIText(Transform parent, string name, string content,
                                     Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos,
                                     TextAnchor alignment, int fontSize = 22)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(
            anchorMin.x == 0 ? 0 : (anchorMin.x == 1 ? 1 : 0.5f),
            anchorMin.y == 0 ? 0 : (anchorMin.y == 1 ? 1 : 0.5f));
        rt.sizeDelta = new Vector2(360, 50);
        rt.anchoredPosition = anchoredPos;

        var text = go.AddComponent<Text>();
        text.text = content;
        text.alignment = alignment;
        text.color = Color.white;
        text.fontSize = fontSize;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return text;
    }

    private static Material LoadOrCreateMaterial(string path, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            EnsureFolder(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
        }
        return mat;
    }

    private static Material LoadOrCreateLineMaterial(string path)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            mat = new Material(shader);
            mat.color = Color.white;
            EnsureFolder(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
        }
        return mat;
    }

    private static void EnsureFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }

    private static bool TagExists(string tag)
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");
        for (int i = 0; i < tagsProp.arraySize; i++)
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return true;
        return false;
    }

    private static void EnsureTag(string tag)
    {
        if (TagExists(tag)) return;
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");
        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
        SerializedProperty newTag = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
        newTag.stringValue = tag;
        tagManager.ApplyModifiedProperties();
        Debug.Log($"[SceneSetupTool] タグ '{tag}' を追加しました。");
    }
}
#endif
