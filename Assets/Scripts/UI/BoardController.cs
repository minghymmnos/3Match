using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StarManor
{
    /// <summary>关卡内总控制器：棋盘表现 + 输入 + 目标/步数 + 胜负流程 + 关卡内 UI。</summary>
    public class BoardController : MonoBehaviour
    {
        private enum State { Idle, Resolving, Winning, Ended, Paused }

        private Canvas _canvas;
        private BoardModel _model;
        private LevelDef _level;
        private State _state = State.Idle;
        private TestLevelConfig _testCfg;   // 场景内测试配置（仅测试关生效）
        private bool _fromTestCfg;          // 本局是否由 TestLevelConfig 自定义生成

        private int _steps;
        private int _continueUsed;

        private RectTransform _boardRoot;
        private readonly Dictionary<Piece, GameObject> _views = new Dictionary<Piece, GameObject>();
        private readonly Dictionary<Vector2Int, GameObject> _obstacleViews = new Dictionary<Vector2Int, GameObject>();
        private Vector2Int? _selected;
        private readonly List<GoalProgress> _goals = new List<GoalProgress>();

        private Text _stepsText;
        private RectTransform _goalsRow;
        private GameObject _modalRoot;

        private float _pitch { get { return GameConfig.CellSize + GameConfig.CellGap; } }

        private class GoalProgress
        {
            public GoalDef def;
            public int progress;
            public Text label;
            public Image icon;
        }

        private void Start()
        {
            _level = LevelDatabase.Get(GameFlow.Instance.CurrentLevelId);
            if (_level == null)
            {
                Debug.LogError("[BoardController] 关卡配置缺失: " + GameFlow.Instance.CurrentLevelId);
                _level = new LevelDef { id = 1, steps = 20, difficulty = "NORMAL", rewardGold = 120 };
            }

            // 测试关 + 场景内挂了 TestLevelConfig：用自定义目标/步数/颜色池覆盖默认测试关
            _testCfg = TestLevelConfig.Instance != null
                ? TestLevelConfig.Instance
                : FindObjectOfType<TestLevelConfig>();
            _fromTestCfg = _testCfg != null && _level.test;
            if (_fromTestCfg) _level = _testCfg.BuildLevelDef();

            _steps = _level.test ? int.MaxValue : _level.steps;
            _continueUsed = SaveSystem.ContinueUsed;
            _continueUsed = 0; // 每次进关重置

            _model = new BoardModel(0);
            if (_fromTestCfg) _model.colorPool = _testCfg.BuildColorPool();
            if (_fromTestCfg) _model.rules = _testCfg.BuildRuleToggles(); // 特殊棋子触发开关仅测试关生效
            if (_level.goals != null)
                foreach (var g in _level.goals)
                    if (g != null && g.type == "collect")
                        _model.preferredGoalColors.Add(PieceColorExt.FromName(g.color));
            _model.Generate(_level);

            BuildUI();
            BuildAllViews();
            RefreshGoalUI();
            RefreshStepsUI();

            // 开局结算：按消除优先级先查直线消除（正常生成棋盘不会有），无待消三连时处理自然形成的 2×2 → 螺旋桨
            var startRuns = _model.FindRuns();
            if (startRuns.Count > 0)
            {
                _state = State.Resolving;
                StartCoroutine(ExecutePlan(_model.PlanFromRuns(startRuns, new Vector2Int(-9, -9), new Vector2Int(-9, -9))));
            }
            else
            {
                var startQuad = _model.NextQuadConvertPlan();
                if (startQuad != null)
                {
                    _state = State.Resolving;
                    StartCoroutine(ExecutePlan(startQuad));
                }
            }

            if (!_level.test && !SaveSystem.UnlimitedLife && SaveSystem.Lives <= 0) ShowNoLifePanel();
        }

        // ---------------------------------------------------------------- UI 构建

        private void BuildUI()
        {
            _canvas = UIFactory.CreateCanvas("GameplayCanvas", transform);

            // 背景（1536x1024 横版素材，等比 Cover 铺满）
            var bg = UIFactory.MakeImage(_canvas.transform, "BG", SpriteLib.BgGarden(), Vector2.zero, Vector2.zero, false, false);
            UIFactory.FitCover(bg.rectTransform, new Vector2(1536, 1024));
            bg.color = new Color(0.62f, 0.62f, 0.68f);

            // 顶栏
            var top = UIFactory.Panel(_canvas.transform, "TopBar",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -170), new Vector2(0, 0),
                new Color(0f, 0f, 0f, 0.18f));
            UIFactory.Text(top, "LevelLabel",
                _fromTestCfg ? "测试关（自定义配置）"
                    : _level.test ? "测试关（无目标 · 步数无限）"
                    : "第 " + _level.id + " 关" + (_level.difficulty == "HARD" ? "（困难）" : ""),
                52, Color.white, new Vector2(600, 70), new Vector2(0, -6));

            // 暂停按钮（锚定顶栏右缘，按钮完整落在栏内，文字提示在按钮左侧）
            var gear = UIFactory.IconButton(top, SpriteLib.IconGear(), new Vector2(92, 92), Vector2.zero, ShowPause);
            var gearRt = (RectTransform)gear.transform;
            gearRt.anchorMin = gearRt.anchorMax = new Vector2(1f, 0.5f);
            gearRt.anchoredPosition = new Vector2(-100, 0);
            var pauseHint = UIFactory.Text(top, "PauseHint", "暂停", 30, Color.white, new Vector2(100, 44), Vector2.zero, TextAnchor.MiddleRight);
            pauseHint.rectTransform.anchorMin = pauseHint.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            pauseHint.rectTransform.anchoredPosition = new Vector2(-162, 0);

            // 目标栏（测试关无目标：显示提示文字代替）
            int n = _level.goals != null ? _level.goals.Length : 0;
            if (n > 0)
            {
            var goalBar = UIFactory.Panel(_canvas.transform, "GoalBar",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-560, -330), new Vector2(560, -190),
                new Color(0f, 0f, 0f, 0.25f));
            _goalsRow = goalBar;
            for (int i = 0; i < n; i++)
            {
                var def = _level.goals[i];
                if (def == null) continue;
                float slotW = 1060f / Mathf.Max(1, n);
                var slot = UIFactory.Anchored(goalBar, "Goal" + i, new Vector2(0f, 0.5f), new Vector2(slotW, 110),
                    new Vector2(slotW * i + slotW * 0.5f, 0));
                Sprite iconSprite = def.type == "collect" ? SpriteLib.Piece(PieceColorExt.FromName(def.color))
                    : (def.target == "crate" ? null : null);
                if (iconSprite != null)
                    UIFactory.MakeImage(slot, "Icon", iconSprite, new Vector2(84, 84), new Vector2(-slotW * 0.5f + 52, 0));
                else
                    UIFactory.Text(slot, "IconTxt", def.type == "clear" && def.target == "crate" ? "箱" : "冰", 44, Color.white,
                        new Vector2(84, 84), new Vector2(-slotW * 0.5f + 52, 0));
                // 目标类型文字说明（收集红花 / 消除木箱 / 清除冰层）
                string goalHint = def.type == "collect"
                    ? "收集" + PieceColorExt.Cn(PieceColorExt.FromName(def.color))
                    : (def.target == "crate" ? "消除木箱" : "清除冰层");
                UIFactory.Text(slot, "GoalHint", goalHint, 26, new Color(1f, 1f, 1f, 0.85f),
                    new Vector2(slotW - 150, 34), new Vector2(44, -32), TextAnchor.MiddleLeft);
                var lbl = UIFactory.Text(slot, "Label", "0/" + def.count, 46, Color.white, new Vector2(slotW - 150, 60),
                    new Vector2(44, 12), TextAnchor.MiddleLeft);
                _goals.Add(new GoalProgress { def = def, progress = 0, label = lbl });
            }
            }
            else
            {
                UIFactory.Text(_canvas.transform, "TestHint", "测试关 · 无目标 · 随意测试", 34,
                    new Color(1f, 1f, 1f, 0.85f), new Vector2(600, 50), new Vector2(0, -260));
            }

            // 步数
            var stepsRoot = UIFactory.Anchored(_canvas.transform, "Steps", new Vector2(0.5f, 1f), new Vector2(200, 150), new Vector2(0, -430));
            UIFactory.Text(stepsRoot, "Caption", "步数", 34, new Color(1f, 1f, 1f, 0.85f), new Vector2(200, 44), new Vector2(0, -44));
            _stepsText = UIFactory.Text(stepsRoot, "Num", _steps.ToString(), 72, Color.white, new Vector2(200, 90), new Vector2(0, 8));

            // 棋盘
            float bw = GameConfig.BoardCols * _pitch - GameConfig.CellGap + 40f;
            float bh = GameConfig.BoardRows * _pitch - GameConfig.CellGap + 40f;
            _boardRoot = UIFactory.Anchored(_canvas.transform, "Board", new Vector2(0.5f, 0.5f), new Vector2(bw, bh), new Vector2(0, -80));
            var boardBg = UIFactory.MakeImage(_boardRoot, "BoardBG", SpriteLib.BoardBase(), new Vector2(bw, bh), Vector2.zero, false, false);
            boardBg.color = new Color(1f, 1f, 1f, 0.92f);

            // 9x9 棋盘格底（清理后的浅/深格图，与棋子逐格对齐）
            for (int x = 0; x < GameConfig.BoardCols; x++)
                for (int y = 0; y < GameConfig.BoardRows; y++)
                {
                    var cellImg = UIFactory.MakeImage(_boardRoot, "Cell_" + x + "_" + y,
                        (x + y) % 2 == 0 ? SpriteLib.BoardCellLight() : SpriteLib.BoardCellDark(),
                        new Vector2(GameConfig.CellSize + 6, GameConfig.CellSize + 6), CellPos(x, y));
                    cellImg.color = new Color(1f, 1f, 1f, 0.95f);
                }
        }

        private Vector2 CellPos(int x, int y)
        {
            float w = _boardRoot.sizeDelta.x - 40f;
            float h = _boardRoot.sizeDelta.y - 40f;
            return new Vector2(-w * 0.5f + _pitch * 0.5f + x * _pitch, -h * 0.5f + _pitch * 0.5f + y * _pitch);
        }

        private Vector2 CellPos(Vector2Int c) { return CellPos(c.x, c.y); }

        // ---------------------------------------------------------------- 棋盘视图

        private void BuildAllViews()
        {
            foreach (var kv in _views) if (kv.Value != null) Destroy(kv.Value);
            _views.Clear();
            foreach (var kv in _obstacleViews) if (kv.Value != null) Destroy(kv.Value);
            _obstacleViews.Clear();
            _selected = null;

            for (int x = 0; x < GameConfig.BoardCols; x++)
                for (int y = 0; y < GameConfig.BoardRows; y++)
                {
                    var p = _model.Get(x, y);
                    if (p != null) CreatePieceView(p, new Vector2Int(x, y));
                    var ob = _model.obstacles[x, y];
                    if (ob != null) CreateObstacleView(new Vector2Int(x, y), ob);
                }
        }

        private GameObject CreatePieceView(Piece p, Vector2Int cell)
        {
            var go = new GameObject("Piece_" + cell.x + "_" + cell.y, typeof(RectTransform), typeof(Image), typeof(PieceInput));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_boardRoot, false);
            rt.sizeDelta = new Vector2(GameConfig.CellSize, GameConfig.CellSize);
            rt.anchoredPosition = CellPos(cell);
            var img = go.GetComponent<Image>();
            img.sprite = SpriteLib.PieceFor(p);
            img.preserveAspect = true;
            img.raycastTarget = true;
            var input = go.GetComponent<PieceInput>();
            input.controller = this;
            input.cell = cell;
            // 素材原图为竖直朝上的火箭：纵向火箭（RocketCol，清整列）保持原样，
            // 横向火箭（RocketRow，清整行）旋转 -90° 使其尖头朝右呈横向
            if (p.special == SpecialKind.RocketRow)
                rt.localRotation = Quaternion.Euler(0, 0, -90);
            _views[p] = go;
            return go;
        }

        private void CreateObstacleView(Vector2Int cell, Obstacle ob)
        {
            GameObject go;
            if (ob.kind == "crate")
            {
                go = new GameObject("Crate_" + cell.x + "_" + cell.y, typeof(RectTransform), typeof(Image));
                var img = go.GetComponent<Image>();
                img.color = new Color(0.55f, 0.38f, 0.2f, 0.96f);
                img.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(_boardRoot, false);
                rt.sizeDelta = new Vector2(GameConfig.CellSize - 6, GameConfig.CellSize - 6);
                rt.anchoredPosition = CellPos(cell);
                var lbl = UIFactory.Text(rt, "HP", ob.hp.ToString(), 44, Color.white, rt.sizeDelta, Vector2.zero);
                lbl.transform.SetAsLastSibling();
            }
            else
            {
                go = new GameObject("Ice_" + cell.x + "_" + cell.y, typeof(RectTransform), typeof(Image));
                var img = go.GetComponent<Image>();
                img.color = new Color(0.72f, 0.88f, 1f, 0.55f);
                img.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(_boardRoot, false);
                rt.sizeDelta = new Vector2(GameConfig.CellSize - 6, GameConfig.CellSize - 6);
                rt.anchoredPosition = CellPos(cell);
            }
            _obstacleViews[cell] = go;
        }

        private void RefreshObstacleView(Vector2Int cell)
        {
            var ob = _model.obstacles[cell.x, cell.y];
            GameObject go;
            if (_obstacleViews.TryGetValue(cell, out go) && go != null)
            {
                if (ob == null) { Destroy(go); _obstacleViews.Remove(cell); }
                else
                {
                    var lbl = go.GetComponentInChildren<Text>();
                    if (lbl != null) lbl.text = ob.hp.ToString();
                }
            }
            else if (ob != null) CreateObstacleView(cell, ob);
        }

        // ---------------------------------------------------------------- 输入

        public void OnPieceDown(Vector2Int cell, Vector2 screenPos)
        {
            if (_state != State.Idle) return;
            if (_selected.HasValue && IsAdjacent(_selected.Value, cell))
            {
                AttemptSwap(_selected.Value, cell);
            }
            else
            {
                SetSelected(cell);
            }
        }

        public void OnSwipe(Vector2Int cell, Vector2Int dir)
        {
            if (_state != State.Idle) return;
            var target = cell + dir;
            if (_model.Get(target.x, target.y) != null) AttemptSwap(cell, target);
        }

        public void OnPieceUp(Vector2Int cell, Vector2 screenPos, bool isClick)
        {
            if (_state != State.Idle || !isClick) return;
            var p = _model.Get(cell.x, cell.y);
            // Royal Match 规则：单击火箭/炸弹/螺旋桨直接激活（不耗步数）；彩球需与相邻棋子交换触发
            // 测试关可通过 TestLevelConfig 按棋子种类关闭单击触发
            if (p != null && p.IsSpecial && !p.IsRainbow && _model.rules.Get(p.special).clickActivate)
            {
                ActivateSpecialAt(cell);
                return;
            }
            if (!_selected.HasValue) SetSelected(cell);
        }

        /// <summary>单击直接激活特殊棋子（火箭/炸弹/螺旋桨/彩球）。不消耗步数。</summary>
        private void ActivateSpecialAt(Vector2Int cell)
        {
            if (_state != State.Idle) return;
            var p = _model.Get(cell.x, cell.y);
            if (p == null || !p.IsSpecial) return;
            if (!_model.rules.Get(p.special).clickActivate) return; // 测试关可按棋子种类关闭单击触发

            _state = State.Resolving;
            SetSelected(null);

            var plan = _model.DetonateAt(cell.x, cell.y);
            if (plan.cells.Count == 0) { _state = State.Idle; return; }

            StartCoroutine(ActivateRoutine(p, cell, plan));
        }

        /// <summary>单击激活演出：特殊棋子脉冲放大 → 飘字提示 → 短暂停顿 → 引爆，让玩家看清激活来源。</summary>
        private IEnumerator ActivateRoutine(Piece p, Vector2Int cell, ClearPlan plan)
        {
            GameObject v;
            if (_views.TryGetValue(p, out v) && v != null)
                yield return StartCoroutine(PulseView(v, 0.45f));

            SoundManager.Instance.Play("combo");
            EffectsRunner.Instance.FloatText(_boardRoot.parent, ComboHint(p.special),
                new Vector2(0, 160), new Color(1f, 0.85f, 0.3f), 56);
            EffectsRunner.Instance.Burst(_boardRoot, CellPos(cell), SpriteLib.PieceFor(p), 1.5f);
            yield return new WaitForSeconds(0.35f); // 飘字与爆光停留

            yield return StartCoroutine(ExecutePlan(plan));
        }

        /// <summary>棋子脉冲：先放大再回弹，用于强调"这颗棋子被激活了"。</summary>
        private IEnumerator PulseView(GameObject v, float dur)
        {
            if (v == null) yield break;
            var rt = v.GetComponent<RectTransform>();
            float t = 0f;
            while (t < dur && rt != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                rt.localScale = Vector3.one * (1f + 0.5f * k);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        private static string ComboHint(SpecialKind k)
        {
            switch (k)
            {
                case SpecialKind.RocketRow: return "横向火箭！消除整行！";
                case SpecialKind.RocketCol: return "纵向火箭！消除整列！";
                case SpecialKind.Bomb: return "炸弹引爆！";
                case SpecialKind.Rainbow: return "彩球发动！";
                default: return "螺旋桨起飞！";
            }
        }

        private void SetSelected(Vector2Int? cell)
        {
            if (_selected.HasValue)
            {
                GameObject v;
                if (_views.TryGetValue(_model.Get(_selected.Value.x, _selected.Value.y), out v) && v != null)
                    v.transform.localScale = Vector3.one;
            }
            _selected = cell;
            if (_selected.HasValue)
            {
                var p = _model.Get(_selected.Value.x, _selected.Value.y);
                GameObject v;
                if (p != null && _views.TryGetValue(p, out v) && v != null)
                    v.transform.localScale = Vector3.one * 1.15f;
            }
        }

        private static bool IsAdjacent(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
        }

        // ---------------------------------------------------------------- 交换与消除主循环

        private void AttemptSwap(Vector2Int a, Vector2Int b)
        {
            if (_state != State.Idle) return;
            if (!IsAdjacent(a, b)) return;
            var pa = _model.Get(a.x, a.y);
            var pb = _model.Get(b.x, b.y);
            if (pa == null || pb == null) return;

            // 关键：必须在模型交换前取视图。TrySwap 会立刻交换模型数据，
            // 交换后再按位置找视图会拿到相反的棋子，导致视图与数据永久错位
            //（表现为图标重叠、看到的连线与实际不符、空位不补全）。
            GameObject va, vb;
            _views.TryGetValue(pa, out va);
            _views.TryGetValue(pb, out vb);

            SetSelected(null);
            var plan = _model.TrySwap(a, b);
            StartCoroutine(SwapAndResolve(a, b, va, vb, plan, plan == null));
        }

        private IEnumerator SwapAndResolve(Vector2Int a, Vector2Int b, GameObject va, GameObject vb, ClearPlan plan, bool bounceBack)
        {
            _state = State.Resolving;
            if (va != null && vb != null)
            {
                var rta = va.GetComponent<RectTransform>();
                var rtb = vb.GetComponent<RectTransform>();
                // 两枚棋子同时向对方位置滑动
                var m1 = StartCoroutine(EffectsRunner.Instance.MoveRect(rta, CellPos(a), CellPos(b), 0.15f));
                var m2 = StartCoroutine(EffectsRunner.Instance.MoveRect(rtb, CellPos(b), CellPos(a), 0.15f));
                yield return m1;
                yield return m2;
                // 更新输入组件的 cell 标记（va = 原 a 处棋子，现位于 b）
                SwapInputCells(va, vb, a, b);
            }

            if (bounceBack || plan == null || plan.cells.Count == 0)
            {
                // 非法交换回弹
                if (va != null && vb != null)
                {
                    var rta = va.GetComponent<RectTransform>();
                    var rtb = vb.GetComponent<RectTransform>();
                    var m1 = StartCoroutine(EffectsRunner.Instance.MoveRect(rta, CellPos(b), CellPos(a), 0.12f));
                    var m2 = StartCoroutine(EffectsRunner.Instance.MoveRect(rtb, CellPos(a), CellPos(b), 0.12f));
                    yield return m1;
                    yield return m2;
                    SwapInputCells(va, vb, a, b);
                    SyncViewsToModel();
                }
                _state = State.Idle;
                yield break;
            }

            if (!_level.test) _steps--; // 测试关步数无限
            RefreshStepsUI();
            SoundManager.Instance.Play("pop");

            yield return StartCoroutine(ExecutePlan(plan));
        }

        /// <summary>执行一个消除方案（组合技演出 → 级联 → 洗牌检查 → 胜负结算）。供交换与单击激活共用。</summary>
        private IEnumerator ExecutePlan(ClearPlan plan)
        {
            if (!string.IsNullOrEmpty(plan.comboName))
            {
                SoundManager.Instance.Play("combo");
                EffectsRunner.Instance.Flash(_canvas, new Color(1f, 1f, 1f, 0.45f), 0.55f);
                EffectsRunner.Instance.FloatText(_boardRoot.parent, plan.comboName, new Vector2(0, 160), new Color(1f, 0.85f, 0.3f), 64);
                EffectsRunner.Instance.RainbowWave(_boardRoot, Vector2.zero);
                yield return new WaitForSeconds(0.45f); // 组合技名称/彩虹波停留，看清是哪种组合再开始消除
            }

            yield return StartCoroutine(CascadeLoop(plan));

            // 结算
            if (!GoalsComplete())
            {
                if (!_model.HasValidMove())
                {
                    if (_model.Shuffle())
                    {
                        EffectsRunner.Instance.FloatText(_boardRoot.parent, "无可消移动，自动洗牌！", new Vector2(0, 100), Color.white, 44);
                        yield return StartCoroutine(RebuildViewsAnimated());
                    }
                    else
                    {
                        Debug.Log("[Board] 洗牌次数用尽，保持当前局面");
                    }
                }
            }

            if (GoalsComplete())
            {
                yield return StartCoroutine(WinBonusSequence());
            }
            else if (_steps <= 0)
            {
                ShowLosePanel();
            }
            else
            {
                _state = State.Idle;
            }
        }

        private void SwapInputCells(GameObject va, GameObject vb, Vector2Int a, Vector2Int b)
        {
            var ia = va != null ? va.GetComponent<PieceInput>() : null;
            var ib = vb != null ? vb.GetComponent<PieceInput>() : null;
            if (ia != null) ia.cell = b;
            if (ib != null) ib.cell = a;
        }

        private IEnumerator CascadeLoop(ClearPlan firstPlan)
        {
            var plan = firstPlan;
            int guard = 0;
            while (plan != null && plan.cells.Count > 0 && guard++ < 200)
            {
                yield return StartCoroutine(ResolvePlan(plan));

                // 消除优先级：先结算直线消除（5连彩球 > 炸弹 > 火箭 > 普通3消），无待消三连时才处理 2×2 自动转化（螺旋桨）
                var runs = _model.FindRuns();
                if (runs.Count > 0)
                {
                    plan = _model.PlanFromRuns(runs, new Vector2Int(-9, -9), new Vector2Int(-9, -9));
                    continue;
                }

                // 场上自然形成的 2×2 同色方块自动转化为螺旋桨（重力补充后新出现的也会被处理）
                plan = _model.NextQuadConvertPlan();
            }
        }

        private IEnumerator ResolvePlan(ClearPlan plan)
        {
            var result = _model.ApplyPlan(plan);

            // 消除表现（按与方案中心的距离做波浪式延迟爆发，玩家能看清消除扩散方向）
            Vector2 centerPos = Vector2.zero;
            int centerCount = 0;
            foreach (var c in plan.cells)
            {
                if (!_model.InBounds(c.x, c.y)) continue;
                centerPos += CellPos(c);
                centerCount++;
            }
            if (centerCount > 0) centerPos /= centerCount;
            foreach (var c in plan.cells)
            {
                if (!_model.InBounds(c.x, c.y)) continue;
                // 找到即将被移除的 view（ApplyPlan 已把格子清空，这里按位置找子物体）
                Vector2 pos = CellPos(c);
                Sprite burstSprite = null;
                var pieceView = FindViewByPosition(pos);
                if (pieceView != null)
                {
                    var img = pieceView.GetComponent<Image>();
                    if (img != null && img.sprite != null) burstSprite = img.sprite;
                    _views.Remove(FindPieceByView(pieceView));
                    Destroy(pieceView);
                }
                if (burstSprite == null) burstSprite = SpriteLib.FxSparkle();
                float dist = Vector2.Distance(pos, centerPos);
                float delay = Mathf.Min(dist * 0.018f, 0.3f);
                EffectsRunner.Instance.BurstDelayed(_boardRoot, pos, burstSprite, 0.8f, delay);
            }

            // 障碍视图全量刷新（螺旋桨直击 / 木箱相邻扣血 / 冰层扣层后 HP 标签与销毁都要即时反映）
            foreach (var cell in new List<Vector2Int>(_obstacleViews.Keys))
                RefreshObstacleView(cell);

            // 新特殊棋子生成表现
            foreach (var sp in plan.spawns)
            {
                if (sp.piece == null || !_model.InBounds(sp.x, sp.y)) continue;
                var oldView = FindViewByPosition(CellPos(new Vector2Int(sp.x, sp.y)));
                if (oldView != null)
                {
                    _views.Remove(FindPieceByView(oldView));
                    Destroy(oldView);
                }
                var v = CreatePieceView(sp.piece, new Vector2Int(sp.x, sp.y));
                StartCoroutine(EffectsRunner.Instance.PopScale(v.GetComponent<RectTransform>(), 0.2f, 0.45f));
            }

            // 目标进度
            foreach (var g in _goals)
            {
                if (g.def.type == "collect")
                {
                    PieceColor c = PieceColorExt.FromName(g.def.color);
                    int n;
                    if (result.collected.TryGetValue(c, out n)) g.progress += n;
                }
                else if (g.def.target == "crate") g.progress += result.cratesDestroyed;
                else if (g.def.target == "ice") g.progress += result.icesDestroyed;
            }
            RefreshGoalUI();

            if (result.totalPieces > 4) SoundManager.Instance.Play("pop");

            yield return new WaitForSeconds(0.3f); // 消除停留：看清哪些棋子消失了

            // 重力下落
            var moves = _model.ApplyGravity();
            yield return StartCoroutine(AnimateGravity(moves));
            // 按模型数据全量校准视图（位置 / cell 标记 / 缺失补建 / 孤儿清理），彻底杜绝错位累积
            SyncViewsToModel();
            yield return new WaitForSeconds(0.03f);
        }

        private IEnumerator AnimateGravity(List<GravityMove> moves)
        {
            if (moves == null || moves.Count == 0) yield break;
            float maxDur = 0.1f;
            foreach (var m in moves)
            {
                GameObject v;
                if (!_views.TryGetValue(m.piece, out v) || v == null)
                {
                    if (m.fromRow < 0)
                    {
                        // 顶部新生成
                        v = CreatePieceView(m.piece, new Vector2Int(m.col, m.toRow));
                        var rt0 = v.GetComponent<RectTransform>();
                        var input0 = v.GetComponent<PieceInput>();
                        input0.cell = new Vector2Int(m.col, m.toRow);
                        var start0 = CellPos(new Vector2Int(m.col, GameConfig.BoardRows + 1));
                        rt0.anchoredPosition = start0;
                        float d0 = 0.08f * (GameConfig.BoardRows + 1 - m.toRow);
                        StartCoroutine(EffectsRunner.Instance.MoveRect(rt0, start0, CellPos(new Vector2Int(m.col, m.toRow)), d0));
                        if (d0 > maxDur) maxDur = d0;
                    }
                    continue;
                }
                var rt = v.GetComponent<RectTransform>();
                var input = v.GetComponent<PieceInput>();
                var from = rt.anchoredPosition;
                var to = CellPos(new Vector2Int(m.col, m.toRow));
                input.cell = new Vector2Int(m.col, m.toRow);
                float d = 0.07f * Mathf.Max(1, (m.fromRow >= 0 ? m.fromRow - m.toRow : 2));
                StartCoroutine(EffectsRunner.Instance.MoveRect(rt, from, to, d));
                if (d > maxDur) maxDur = d;
            }
            yield return new WaitForSeconds(maxDur + 0.05f);
        }

        /// <summary>
        /// 以模型数据为唯一事实源，全量校准棋子视图：
        /// 位置吸附到 CellPos、input.cell 同步、缺失视图补建、孤儿/重复视图清除。
        /// </summary>
        private void SyncViewsToModel()
        {
            var onGrid = new HashSet<Piece>();
            for (int x = 0; x < GameConfig.BoardCols; x++)
                for (int y = 0; y < GameConfig.BoardRows; y++)
                {
                    var p = _model.Get(x, y);
                    if (p == null) continue;
                    onGrid.Add(p);
                    GameObject v;
                    if (_views.TryGetValue(p, out v) && v != null)
                    {
                        var rt = v.GetComponent<RectTransform>();
                        rt.anchoredPosition = CellPos(x, y);
                        // 素材为竖直火箭：横向火箭旋转 -90° 呈横向，纵向火箭保持原样
                        rt.localRotation = p.special == SpecialKind.RocketRow
                            ? Quaternion.Euler(0, 0, -90) : Quaternion.identity;
                        v.GetComponent<PieceInput>().cell = new Vector2Int(x, y);
                    }
                    else
                    {
                        CreatePieceView(p, new Vector2Int(x, y)); // 缺视图兜底
                    }
                }
            // 清理不在棋盘上的孤儿视图（防止重复/残留导致图标重叠）
            List<Piece> orphans = null;
            foreach (var kv in _views)
                if (kv.Value == null || !onGrid.Contains(kv.Key))
                    (orphans ?? (orphans = new List<Piece>())).Add(kv.Key);
            if (orphans != null)
                foreach (var k in orphans)
                {
                    if (_views[k] != null) Destroy(_views[k]);
                    _views.Remove(k);
                }
        }

        private Piece FindPieceByView(GameObject view)
        {
            foreach (var kv in _views)
                if (kv.Value == view) return kv.Key;
            return null;
        }

        private GameObject FindViewByPosition(Vector2 localPos)
        {
            GameObject best = null;
            float bestDist = 90f * 90f;
            foreach (var kv in _views)
            {
                if (kv.Value == null) continue;
                float d = (kv.Value.GetComponent<RectTransform>().anchoredPosition - localPos).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = kv.Value; }
            }
            return best;
        }

        private IEnumerator RebuildViewsAnimated()
        {
            BuildAllViews();
            foreach (var kv in _views)
            {
                if (kv.Value != null)
                    StartCoroutine(EffectsRunner.Instance.PopScale(kv.Value.GetComponent<RectTransform>(), 0.1f, 0.2f));
            }
            yield return new WaitForSeconds(0.25f);
        }

        // ---------------------------------------------------------------- 目标 / 步数

        private bool GoalsComplete()
        {
            foreach (var g in _goals)
                if (g.progress < g.def.count) return false;
            return _goals.Count > 0;
        }

        private void RefreshGoalUI()
        {
            foreach (var g in _goals)
                if (g.label != null)
                {
                    g.label.text = Mathf.Min(g.progress, g.def.count) + "/" + g.def.count;
                    g.label.color = g.progress >= g.def.count ? new Color(0.5f, 1f, 0.6f) : Color.white;
                }
        }

        private void RefreshStepsUI()
        {
            if (_stepsText != null)
            {
                if (_level.test)
                {
                    _stepsText.text = "∞";
                    _stepsText.color = Color.white;
                    return;
                }
                _stepsText.text = _steps.ToString();
                _stepsText.color = _steps <= 5 ? new Color(1f, 0.35f, 0.3f) : Color.white;
            }
        }

        private void Update()
        {
            if (_stepsText != null && _steps <= 5 && _state == State.Idle)
            {
                float k = 1f + 0.06f * Mathf.Sin(Time.time * 8f);
                _stepsText.rectTransform.localScale = Vector3.one * k;
            }
        }

        // ---------------------------------------------------------------- 胜利

        private IEnumerator WinBonusSequence()
        {
            _state = State.Winning;
            // 测试关步数为 ∞：剩余步数奖励封顶 5，避免胜利演出无限循环
            int bonusSteps = _level.test ? Mathf.Min(_steps, 5) : _steps;
            _steps = 0;
            RefreshStepsUI();
            if (bonusSteps > 0)
                EffectsRunner.Instance.FloatText(_boardRoot.parent, "剩余步数奖励！", new Vector2(0, 160), new Color(1f, 0.85f, 0.3f), 56);

            int guard = 0;
            while (bonusSteps > 0 && _model.AllPieceCells().Count > 0 && guard++ < 60)
            {
                bonusSteps--;
                var cells = _model.AllPieceCells();
                var c = cells[_model.rng.Next(cells.Count)];
                _model.MakeBonusSpecialAt(c.x, c.y);
                yield return new WaitForSeconds(0.12f);
                var plan = _model.DetonateAt(c.x, c.y);
                if (plan.cells.Count > 0)
                    yield return StartCoroutine(ResolvePlan(plan));
                // 连锁
                while (true)
                {
                    var runs = _model.FindRuns();
                    if (runs.Count == 0) break;
                    var p = _model.PlanFromRuns(runs, new Vector2Int(-9, -9), new Vector2Int(-9, -9));
                    yield return StartCoroutine(ResolvePlan(p));
                }
            }

            SoundManager.Instance.Play("win");

            // 结算奖励
            bool firstClear = GameFlow.Instance.CurrentLevelId >= SaveSystem.MaxUnlockedLevel;
            int gold = _level.rewardGold > 0 ? _level.rewardGold : GameConfig.RewardGoldNormal;
            if (_level.difficulty == "HARD") gold = GameConfig.RewardGoldHard;
            SaveSystem.AddGold(gold);
            if (firstClear)
            {
                SaveSystem.AddStars(1);
                SaveSystem.MaxUnlockedLevel = Mathf.Max(SaveSystem.MaxUnlockedLevel, GameFlow.Instance.CurrentLevelId + 1);
            }
            SaveSystem.Save();

            yield return new WaitForSeconds(0.4f);
            ShowWinPanel(gold, firstClear);
        }

        private void ShowWinPanel(int gold, bool firstClear)
        {
            _state = State.Ended;
            var modal = CreateModal("WinPanel");
            var rt = modal.GetComponent<RectTransform>();
            StartCoroutine(EffectsRunner.Instance.PopScale(rt));

            UIFactory.Text(modal, "Title", "通关成功！", 76, new Color(1f, 0.85f, 0.3f), new Vector2(700, 110), new Vector2(0, 440));
            UIFactory.Text(modal, "Reward", "金币 +" + gold + (firstClear ? "   星星 +1" : ""), 52, Color.white, new Vector2(700, 80), new Vector2(0, 220));
            var starIcon = UIFactory.MakeImage(modal, "Star", SpriteLib.IconStar(), new Vector2(150, 150), new Vector2(0, 90));

            bool hasNext = LevelDatabase.Get(GameFlow.Instance.CurrentLevelId + 1) != null;
            if (hasNext)
                UIFactory.TextButton(modal, "下一关", 56, new Vector2(420, 120), new Vector2(0, -80), () =>
                {
                    CloseModal(modal);
                    GameFlow.Instance.StartLevel(GameFlow.Instance.CurrentLevelId + 1);
                });
            else
                UIFactory.Text(modal, "AllDone", "已通关全部关卡！", 48, Color.white, new Vector2(700, 70), new Vector2(0, -80));

            UIFactory.TextButton(modal, "返回庄园", 52, new Vector2(420, 120), new Vector2(0, -230), () =>
            {
                CloseModal(modal);
                GameFlow.Instance.BackToMenu();
            });
        }

        // ---------------------------------------------------------------- 失败 / 暂停 / 生命

        private void ShowLosePanel()
        {
            _state = State.Ended;
            SoundManager.Instance.Play("fail");
            if (!_fromTestCfg) SaveSystem.ConsumeLifeOnFail(); // 失败扣 1 生命（策划案 §6.3）；测试自定义关不扣

            var modal = CreateModal("LosePanel");
            StartCoroutine(EffectsRunner.Instance.PopScale(modal.GetComponent<RectTransform>()));

            UIFactory.Text(modal, "Title", "差一点就成功了！", 64, Color.white, new Vector2(700, 100), new Vector2(0, 440));
            string remain = "";
            foreach (var g in _goals)
                if (g.progress < g.def.count)
                    remain += (g.def.type == "collect" ? PieceColorExt.Cn(PieceColorExt.FromName(g.def.color)) : (g.def.target == "crate" ? "木箱" : "冰层"))
                              + " " + Mathf.Min(g.progress, g.def.count) + "/" + g.def.count + "  ";
            UIFactory.Text(modal, "Remain", remain, 46, new Color(1f, 1f, 1f, 0.85f), new Vector2(700, 70), new Vector2(0, 230));

            bool canContinue = SaveSystem.Gold >= GameConfig.ContinueCostGold && _continueUsed < GameConfig.ContinueLimitPerLevel;
            var btn = UIFactory.TextButton(modal,
                canContinue ? "+5 步（" + GameConfig.ContinueCostGold + " 金币）" : "金币不足",
                50, new Vector2(520, 120), new Vector2(0, 90),
                () =>
                {
                    if (!canContinue) return;
                    SaveSystem.AddGold(-GameConfig.ContinueCostGold);
                    _continueUsed++;
                    SaveSystem.ContinueUsed = _continueUsed;
                    _steps += GameConfig.ContinueExtraSteps;
                    RefreshStepsUI();
                    CloseModal(modal);
                    _state = State.Idle;
                }, canContinue ? null : SpriteLib.BtnDisabled());
            if (!canContinue) btn.interactable = false;

            UIFactory.TextButton(modal, "重新挑战", 52, new Vector2(520, 120), new Vector2(0, -70), () =>
            {
                CloseModal(modal);
                GameFlow.Instance.StartLevel(GameFlow.Instance.CurrentLevelId);
            });
            UIFactory.TextButton(modal, "返回庄园", 52, new Vector2(520, 120), new Vector2(0, -240), () =>
            {
                CloseModal(modal);
                GameFlow.Instance.BackToMenu();
            });
        }

        private void ShowPause()
        {
            if (_state != State.Idle && _state != State.Paused) return;
            _state = State.Paused;
            Time.timeScale = 0f;
            var modal = CreateModal("PausePanel");
            UIFactory.Text(modal, "Title", "暂停", 72, Color.white, new Vector2(700, 100), new Vector2(0, 440));
            UIFactory.TextButton(modal, "继续游戏", 52, new Vector2(520, 120), new Vector2(0, 140), () =>
            {
                Time.timeScale = 1f;
                CloseModal(modal);
                _state = State.Idle;
            });
            UIFactory.TextButton(modal, "重新开始", 52, new Vector2(520, 120), new Vector2(0, 0), () =>
            {
                Time.timeScale = 1f;
                GameFlow.Instance.StartLevel(GameFlow.Instance.CurrentLevelId);
            });
            UIFactory.TextButton(modal, SaveSystem.SoundOn ? "音效：开" : "音效：关", 48, new Vector2(520, 120), new Vector2(0, -140), () =>
            {
                SaveSystem.SoundOn = !SaveSystem.SoundOn;
                Time.timeScale = 1f;
                GameFlow.Instance.StartLevel(GameFlow.Instance.CurrentLevelId);
            });
            UIFactory.TextButton(modal, "退出关卡", 52, new Vector2(520, 120), new Vector2(0, -280), () =>
            {
                Time.timeScale = 1f;
                GameFlow.Instance.BackToMenu(); // 主动退出不扣生命
            });
        }

        private void ShowNoLifePanel()
        {
            var modal = CreateModal("NoLifePanel");
            UIFactory.Text(modal, "Title", "生命不足", 72, Color.white, new Vector2(700, 100), new Vector2(0, 430));
            UIFactory.Text(modal, "Info", "生命每 " + GameConfig.LifeRegenMinutes + " 分钟恢复 1 点", 46,
                new Color(1f, 1f, 1f, 0.85f), new Vector2(700, 70), new Vector2(0, 180));
            UIFactory.TextButton(modal, "测试：+1 生命", 50, new Vector2(520, 120), new Vector2(0, 30), () =>
            {
                SaveSystem.GrantLife();
                CloseModal(modal);
                _state = State.Idle;
            });
            UIFactory.TextButton(modal, "返回庄园", 52, new Vector2(520, 120), new Vector2(0, -130), () =>
            {
                CloseModal(modal);
                GameFlow.Instance.BackToMenu();
            });
        }

        // ---------------------------------------------------------------- 弹窗基建

        private GameObject CreateModal(string name)
        {
            var dim = UIFactory.Panel(_canvas.transform, name + "_Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.55f));
            dim.GetComponent<Image>().raycastTarget = true; // 阻挡下层输入
            var modal = UIFactory.MakeImage(dim.transform, name, SpriteLib.PanelBase(), new Vector2(880, 1100), Vector2.zero, true, false);
            modal.type = Image.Type.Simple;
            modal.raycastTarget = true;
            _modalRoot = dim.gameObject;
            return modal.gameObject;
        }

        private void CloseModal(GameObject modal)
        {
            if (modal != null) Destroy(modal.transform.parent.gameObject);
            _modalRoot = null;
        }
    }
}
