using System.Collections.Generic;
using UnityEngine;

namespace StarManor
{
    /// <summary>棋子：颜色 + 特殊类型（纯数据）。</summary>
    public class Piece
    {
        public PieceColor color;
        public SpecialKind special;

        public bool IsRainbow { get { return special == SpecialKind.Rainbow; } }
        public bool HasColor { get { return special != SpecialKind.Rainbow; } }
        public bool IsSpecial { get { return special != SpecialKind.None; } }

        public Piece(PieceColor c, SpecialKind s = SpecialKind.None)
        {
            color = c;
            special = s;
        }

        public Piece Clone() { return new Piece(color, special); }
    }

    /// <summary>障碍物（木箱/冰层）。</summary>
    public class Obstacle
    {
        public string kind; // "crate" | "ice"
        public int hp;
        public Obstacle(string k, int h) { kind = k; hp = h; }
    }

        /// <summary>消除方案中要生成的新特殊棋子。</summary>
        public class SpecialSpawn
        {
            public int x, y;
            public Piece piece;
        }

        /// <summary>一次消除的完整方案。</summary>
        public class ClearPlan
        {
            public HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
            public List<SpecialSpawn> spawns = new List<SpecialSpawn>();
            public string comboName; // 组合技提示（如“火箭+炸弹”）
            /// <summary>螺旋桨对障碍物的直接打击格（hp-1，不通过相邻消除）。</summary>
            public List<Vector2Int> propellerDirectHits = new List<Vector2Int>();
        }

    /// <summary>消除结算结果。</summary>
    public class ClearResult
    {
        public Dictionary<PieceColor, int> collected = new Dictionary<PieceColor, int>();
        public int cratesDestroyed;
        public int icesDestroyed;
        public int totalPieces;
    }

    /// <summary>重力下落的单步移动（供表现层做动画）。</summary>
    public class GravityMove
    {
        public int col;
        public int fromRow; // -1 表示从棋盘顶部新生成
        public int toRow;
        public Piece piece;
    }

        /// <summary>9×9 三消棋盘纯数据模型（不含表现与协程）。</summary>
        public class BoardModel
    {
        public Piece[,] grid;        // [col, row]，row 0 = 底部
        public Obstacle[,] obstacles;
        public System.Random rng;
        public int shuffleCount;

        /// <summary>测试用棋子颜色池（null 或空 = 全部 6 色）。减少颜色种类更容易凑出特殊棋子。</summary>
        public List<PieceColor> colorPool;

        /// <summary>
        /// 特殊棋子触发开关（仅测试关由 TestLevelConfig 关闭；正式关永远全开）。
        /// 只拦截"主动触发"通道：单击 / 与普通棋子交换 / 特殊棋子之间交换（组合技）。
        /// 不影响消除机制本身：特殊棋子被普通消除波及时的连锁引爆（ExpandChain）保持原样。
        /// 每类特殊棋子（火箭/炸弹/螺旋桨/彩球）三个通道独立开关。
        /// </summary>
        [System.Serializable]
        public class PieceRule
        {
            public bool clickActivate = true;   // 单击触发
            public bool swapWithNormal = true;  // 与普通棋子交换触发
            public bool swapSpecials = true;    // 与其他特殊棋子交换触发（组合技）
        }

        [System.Serializable]
        public class RuleToggles
        {
            public PieceRule rocket = new PieceRule();     // 纵向/横向火箭共用
            public PieceRule bomb = new PieceRule();
            public PieceRule propeller = new PieceRule();
            public PieceRule rainbow = new PieceRule();

            public PieceRule Get(SpecialKind kind)
            {
                switch (kind)
                {
                    case SpecialKind.RocketRow:
                    case SpecialKind.RocketCol: return rocket;
                    case SpecialKind.Bomb: return bomb;
                    case SpecialKind.Propeller: return propeller;
                    case SpecialKind.Rainbow: return rainbow;
                    default: return new PieceRule(); // None：不会被查询到
                }
            }
        }

        public RuleToggles rules = new RuleToggles();

        public BoardModel(int seed)
        {
            grid = new Piece[GameConfig.BoardCols, GameConfig.BoardRows];
            obstacles = new Obstacle[GameConfig.BoardCols, GameConfig.BoardRows];
            rng = new System.Random(seed == 0 ? (int)System.DateTime.Now.Ticks : seed);
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && x < GameConfig.BoardCols && y >= 0 && y < GameConfig.BoardRows;
        }

        public Piece Get(int x, int y) { return InBounds(x, y) ? grid[x, y] : null; }

        public bool HasCrate(int x, int y)
        {
            return InBounds(x, y) && obstacles[x, y] != null && obstacles[x, y].kind == "crate";
        }

        public bool HasIce(int x, int y)
        {
            return InBounds(x, y) && obstacles[x, y] != null && obstacles[x, y].kind == "ice";
        }

        // ---------------------------------------------------------------- 生成

        /// <summary>生成初始棋盘（无初始三连、至少一个可用移动）。</summary>
        public void Generate(LevelDef level)
        {
            shuffleCount = 0;
            for (int x = 0; x < GameConfig.BoardCols; x++)
                for (int y = 0; y < GameConfig.BoardRows; y++)
                {
                    grid[x, y] = null;
                    obstacles[x, y] = null;
                }

            if (level != null)
            {
                if (level.crates != null)
                    foreach (var c in level.crates)
                        if (InBounds(c.x, c.y)) obstacles[c.x, c.y] = new Obstacle("crate", Mathf.Max(1, c.hp));
                if (level.ices != null)
                    foreach (var c in level.ices)
                        if (InBounds(c.x, c.y)) obstacles[c.x, c.y] = new Obstacle("ice", Mathf.Max(1, c.hp));
            }

            for (int x = 0; x < GameConfig.BoardCols; x++)
                for (int y = 0; y < GameConfig.BoardRows; y++)
                {
                    if (HasCrate(x, y)) continue; // 木箱格不生成棋子
                    grid[x, y] = new Piece(RandomColorNoMatch(x, y));
                }

            if (!HasValidMove()) Shuffle();
        }

        /// <summary>从颜色池随机取色（池为空时回退全 6 色）。</summary>
        private PieceColor PoolColor()
        {
            if (colorPool == null || colorPool.Count == 0) return (PieceColor)rng.Next(6);
            return colorPool[rng.Next(colorPool.Count)];
        }

        private PieceColor RandomColorNoMatch(int x, int y)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var c = PoolColor();
                // 检查左侧两个与下方两个是否构成三连
                if (x >= 2 && grid[x - 1, y] != null && grid[x - 2, y] != null &&
                    grid[x - 1, y].color == c && grid[x - 2, y].color == c) continue;
                if (y >= 2 && grid[x, y - 1] != null && grid[x, y - 2] != null &&
                    grid[x, y - 1].color == c && grid[x, y - 2].color == c) continue;
                return c;
            }
            return (PieceColor)rng.Next(6);
        }

        // ---------------------------------------------------------------- 匹配检测

        public class Run
        {
            public bool horiz;
            public int x, y, len;
            public PieceColor color;
        }

        /// <summary>找出所有长度 ≥3 的横/竖同色连排（跳过木箱格与彩虹球）。</summary>
        public List<Run> FindRuns()
        {
            var runs = new List<Run>();
            // 横向
            for (int y = 0; y < GameConfig.BoardRows; y++)
            {
                int runStart = 0;
                for (int x = 1; x <= GameConfig.BoardCols; x++)
                {
                    bool same = false;
                    if (x < GameConfig.BoardCols)
                    {
                        var a = grid[x - 1, y]; var b = grid[x, y];
                        same = a != null && b != null && a.HasColor && b.HasColor && a.color == b.color;
                    }
                    if (!same)
                    {
                        int len = x - runStart;
                        if (len >= 3)
                        {
                            var p = grid[runStart, y];
                            if (p != null && p.HasColor)
                                runs.Add(new Run { horiz = true, x = runStart, y = y, len = len, color = p.color });
                        }
                        runStart = x;
                    }
                }
            }
            // 纵向
            for (int x = 0; x < GameConfig.BoardCols; x++)
            {
                int runStart = 0;
                for (int y = 1; y <= GameConfig.BoardRows; y++)
                {
                    bool same = false;
                    if (y < GameConfig.BoardRows)
                    {
                        var a = grid[x, y - 1]; var b = grid[x, y];
                        same = a != null && b != null && a.HasColor && b.HasColor && a.color == b.color;
                    }
                    if (!same)
                    {
                        int len = y - runStart;
                        if (len >= 3)
                        {
                            var p = grid[x, runStart];
                            if (p != null && p.HasColor)
                                runs.Add(new Run { horiz = false, x = x, y = runStart, len = len, color = p.color });
                        }
                        runStart = y;
                    }
                }
            }
            return runs;
        }

        public bool HasMatch() { return FindRuns().Count > 0; }

        // ---------------------------------------------------------------- 交换与方案构建

        /// <summary>交换两点（不校验）。</summary>
        private void Swap(int ax, int ay, int bx, int by)
        {
            var t = grid[ax, ay];
            grid[ax, ay] = grid[bx, by];
            grid[bx, by] = t;
        }

        /// <summary>
        /// 尝试一次玩家交换。返回 null 表示非法交换（回弹）。
        /// a = 拖动的棋子原位置, b = 目标位置。
        /// </summary>
        public ClearPlan TrySwap(Vector2Int a, Vector2Int b)
        {
            var pa = Get(a.x, a.y);
            var pb = Get(b.x, b.y);
            if (pa == null || pb == null) return null;

            // 组合技：任一为彩虹球，或两个都是特殊棋子（触发类别归"特殊+特殊"）；
            // 彩球+普通棋子归"特殊+普通"类别。按参与棋子各自的开关判断，关闭时不触发组合，按普通交换流程继续。
            if (pa.IsRainbow || pb.IsRainbow || (pa.IsSpecial && pb.IsSpecial))
            {
                bool comboAllowed;
                if (pa.IsRainbow && pb.IsRainbow)
                    comboAllowed = rules.Get(SpecialKind.Rainbow).swapSpecials;
                else if (pa.IsRainbow || pb.IsRainbow)
                {
                    var other = pa.IsRainbow ? pb : pa;
                    comboAllowed = other.IsSpecial
                        ? (rules.Get(SpecialKind.Rainbow).swapSpecials && rules.Get(other.special).swapSpecials)
                        : rules.Get(SpecialKind.Rainbow).swapWithNormal;
                }
                else comboAllowed = rules.Get(pa.special).swapSpecials && rules.Get(pb.special).swapSpecials;
                if (comboAllowed) return BuildComboPlan(a, b);
                // 开关关闭：落到下方普通交换逻辑（彩球无颜色不会构成消除，通常回弹）
            }

            Swap(a.x, a.y, b.x, b.y);
            var runs = FindRuns();

            // 主动构造 2×2：交换落点处形成独立 2×2 同色方块 → 四格消除 + 落点生成螺旋桨
            var runCells = RunCells(runs);
            bool hasQuadB = TryGetIndependentQuad(b, runCells, out var quadB, out var quadColorB);
            bool hasQuadA = TryGetIndependentQuad(a, runCells, out var quadA, out var quadColorA);
            if (hasQuadB && hasQuadA && quadA[0] == quadB[0]) hasQuadA = false; // a、b 属于同一个 2×2 时只生成一个

            if (runs.Count == 0)
            {
                // 特殊棋子 + 普通棋子且未构成消除：该特殊棋子的"与普通交换"开关开启时，在落点直接激活（特殊优先于 2×2）。
                // 特殊+特殊组合被关闭时不在此处单独引爆，直接走 2×2/回弹。
                if (pa.IsSpecial && !pb.IsSpecial && rules.Get(pa.special).swapWithNormal)
                    return PlanSingleDetonation(b, pa);
                if (pb.IsSpecial && !pa.IsSpecial && rules.Get(pb.special).swapWithNormal)
                    return PlanSingleDetonation(a, pb);
                if (!hasQuadB && !hasQuadA)
                {
                    Swap(a.x, a.y, b.x, b.y); // 回弹
                    return null;
                }
            }
            var plan = PlanFromRuns(runs, b, a);
            if (hasQuadB) AddQuadSpawn(plan, quadB, b, quadColorB);
            if (hasQuadA) AddQuadSpawn(plan, quadA, a, quadColorA);
            // 特殊棋子交换触发（即使交换同时构成了消除也在落点触发）：按触发棋子自身的开关判断。
            //（火箭=落点行/列、炸弹=落点 5×5、螺旋桨=自动消除一个目标；拖动或被换两个方向均生效。
            //  若特殊棋子本身参与了消除，ExpandChain 已触发过，HashSet 去重不会重复引爆。）
            if (pa.IsSpecial && pb.IsSpecial)
            {
                // 特殊+特殊：双方"与其他特殊交换"开关都开启才在落点引爆
                if (rules.Get(pa.special).swapSpecials && rules.Get(pb.special).swapSpecials)
                    MergeSingleDetonation(plan, b, pa);
            }
            else if (pa.IsSpecial && rules.Get(pa.special).swapWithNormal)
                MergeSingleDetonation(plan, b, pa);
            else if (pb.IsSpecial && rules.Get(pb.special).swapWithNormal)
                MergeSingleDetonation(plan, a, pb);
            return plan;
        }

        /// <summary>run 列表展开为格子列表。</summary>
        private List<Vector2Int> RunCells(List<Run> runs)
        {
            var cells = new List<Vector2Int>();
            if (runs == null) return cells;
            foreach (var r in runs)
                for (int i = 0; i < r.len; i++)
                    cells.Add(r.horiz ? new Vector2Int(r.x + i, r.y) : new Vector2Int(r.x, r.y + i));
            return cells;
        }

        /// <summary>
        /// 找包含 pos 的独立 2×2 同色方块（四格均不属于任何直线消除）。
        /// 用于"移动棋子主动构造 2×2 生成螺旋桨"。找到返回四格与颜色。
        /// </summary>
        private bool TryGetIndependentQuad(Vector2Int pos, List<Vector2Int> runCells, out Vector2Int[] quad, out PieceColor color)
        {
            quad = null;
            color = PieceColor.Rose;
            for (int dx = -1; dx <= 0; dx++)
                for (int dy = -1; dy <= 0; dy++)
                {
                    int x0 = pos.x + dx, y0 = pos.y + dy;
                    if (x0 < 0 || y0 < 0 || x0 + 1 >= GameConfig.BoardCols || y0 + 1 >= GameConfig.BoardRows) continue;
                    var c00 = new Vector2Int(x0, y0); var c10 = new Vector2Int(x0 + 1, y0);
                    var c01 = new Vector2Int(x0, y0 + 1); var c11 = new Vector2Int(x0 + 1, y0 + 1);
                    if (runCells.Contains(c00) || runCells.Contains(c10) ||
                        runCells.Contains(c01) || runCells.Contains(c11)) continue;
                    var p0 = grid[x0, y0]; var p1 = grid[x0 + 1, y0];
                    var p2 = grid[x0, y0 + 1]; var p3 = grid[x0 + 1, y0 + 1];
                    if (p0 == null || p1 == null || p2 == null || p3 == null) continue;
                    if (!p0.HasColor || p1.color != p0.color || p2.color != p0.color || p3.color != p0.color) continue;
                    quad = new[] { c00, c10, c01, c11 };
                    color = p0.color;
                    return true;
                }
            return false;
        }

        /// <summary>把主动构造的 2×2 并入消除方案：四格清除 + 在落点生成螺旋桨。</summary>
        private void AddQuadSpawn(ClearPlan plan, Vector2Int[] quad, Vector2Int spawnPos, PieceColor color)
        {
            foreach (var c in quad) plan.cells.Add(c);
            plan.spawns.Add(new SpecialSpawn { x = spawnPos.x, y = spawnPos.y, piece = new Piece(color, SpecialKind.Propeller) });
            ExpandChain(plan); // 方块内若含特殊棋子（如同色火箭），照常连锁引爆
        }

        /// <summary>
        /// 场上自动转化：找一个 2×2 同色方块，返回"四格清除 + 方块原点生成螺旋桨"的方案。
        /// 供开局与每次级联落定后调用（此时盘面无三连，任何 2×2 均为独立方块）；无则返回 null。
        /// </summary>
        public ClearPlan NextQuadConvertPlan()
        {
            for (int x = 0; x < GameConfig.BoardCols - 1; x++)
                for (int y = 0; y < GameConfig.BoardRows - 1; y++)
                {
                    var p0 = grid[x, y]; var p1 = grid[x + 1, y];
                    var p2 = grid[x, y + 1]; var p3 = grid[x + 1, y + 1];
                    if (p0 == null || p1 == null || p2 == null || p3 == null) continue;
                    if (!p0.HasColor || p1.color != p0.color || p2.color != p0.color || p3.color != p0.color) continue;
                    var plan = new ClearPlan();
                    plan.cells.Add(new Vector2Int(x, y));
                    plan.cells.Add(new Vector2Int(x + 1, y));
                    plan.cells.Add(new Vector2Int(x, y + 1));
                    plan.cells.Add(new Vector2Int(x + 1, y + 1));
                    plan.spawns.Add(new SpecialSpawn { x = x, y = y, piece = new Piece(p0.color, SpecialKind.Propeller) });
                    ExpandChain(plan);
                    return plan;
                }
            return null;
        }

        /// <summary>把一枚特殊棋子在 pos 处的引爆并入已有方案（不覆盖 comboName，去重后连锁展开）。</summary>
        private void MergeSingleDetonation(ClearPlan plan, Vector2Int pos, Piece p)
        {
            if (!InBounds(pos.x, pos.y) || !plan.cells.Add(pos)) return;
            foreach (var e in DetonationCells(pos, p, plan))
                if (InBounds(e.x, e.y)) plan.cells.Add(e);
            ExpandChain(plan);
        }

        /// <summary>单个特殊棋子在 pos 处激活的方案（用于交换激活）。</summary>
        private ClearPlan PlanSingleDetonation(Vector2Int pos, Piece p)
        {
            var plan = new ClearPlan();
            switch (p.special)
            {
                case SpecialKind.RocketRow: plan.comboName = "横向火箭！消除整行！"; break;
                case SpecialKind.RocketCol: plan.comboName = "纵向火箭！消除整列！"; break;
                case SpecialKind.Bomb: plan.comboName = "炸弹引爆！"; break;
                case SpecialKind.Propeller: plan.comboName = "螺旋桨起飞！"; break;
            }
            plan.cells.Add(pos);
            foreach (var e in DetonationCells(pos, p, plan))
                if (InBounds(e.x, e.y)) plan.cells.Add(e);
            ExpandChain(plan);
            return plan;
        }

        /// <summary>根据 runs 构建消除方案（含特殊棋子生成判定，优先级：5连>T/L>4连>2×2）。</summary>
        public ClearPlan PlanFromRuns(List<Run> runs, Vector2Int swapPos, Vector2Int otherSwapPos)
        {
            var plan = new ClearPlan();
            var claimed = new HashSet<Vector2Int>();
            var involved = new HashSet<Vector2Int>();

            foreach (var r in runs)
                for (int i = 0; i < r.len; i++)
                    involved.Add(r.horiz ? new Vector2Int(r.x + i, r.y) : new Vector2Int(r.x, r.y + i));

            var consumed = new HashSet<int>(); // 已被更高优先级特殊棋子占用的 run，避免重复生成

            // 1) 直线 5+ 连 → 彩球
            for (int ri = 0; ri < runs.Count; ri++)
            {
                var r = runs[ri];
                if (r.len < 5) continue;
                Vector2Int pos = new Vector2Int(r.x + r.len / 2, r.y);
                if (!r.horiz) pos = new Vector2Int(r.x, r.y + r.len / 2);
                if (involved.Contains(swapPos) && SameLine(r, swapPos)) pos = swapPos;
                pos = PickSpawnInRun(r, pos, claimed);
                if (pos.x < 0) continue;
                claimed.Add(pos);
                consumed.Add(ri);
                plan.spawns.Add(new SpecialSpawn { x = pos.x, y = pos.y, piece = new Piece(PieceColor.Rose, SpecialKind.Rainbow) });
            }

            // 2) T/L 交叉（同一颜色的横竖 run 交叉且合计 ≥5 格）→ 炸弹
            for (int i = 0; i < runs.Count; i++)
            {
                for (int j = i + 1; j < runs.Count; j++)
                {
                    if (consumed.Contains(i) || consumed.Contains(j)) continue;
                    var h = runs[i].horiz ? runs[i] : runs[j];
                    var v = runs[i].horiz ? runs[j] : runs[i];
                    if (!h.horiz || v.horiz) continue;
                    if (h.color != v.color) continue;
                    Vector2Int cross = new Vector2Int(-1, -1);
                    for (int a = 0; a < h.len && cross.x < 0; a++)
                    {
                        int cx = h.x + a;
                        for (int b = 0; b < v.len; b++)
                        {
                            if (v.x == cx && v.y + b == h.y) { cross = new Vector2Int(cx, h.y); break; }
                        }
                    }
                    if (cross.x < 0) continue; // 不相交
                    int total = h.len + v.len - 1;
                    if (total < 5) continue;
                    if (claimed.Contains(cross)) continue;
                    claimed.Add(cross);
                    consumed.Add(i); consumed.Add(j);
                    plan.spawns.Add(new SpecialSpawn { x = cross.x, y = cross.y, piece = new Piece(h.color, SpecialKind.Bomb) });
                }
            }

            // 3) 4 连 → 火箭：四个一行 → 纵向火箭（清一列）；四个一列 → 横向火箭（清一行）
            for (int ri = 0; ri < runs.Count; ri++)
            {
                var r = runs[ri];
                if (r.len != 4 || consumed.Contains(ri)) continue;
                Vector2Int pos = new Vector2Int(r.x + 1, r.y);
                if (!r.horiz) pos = new Vector2Int(r.x, r.y + 1);
                if (involved.Contains(swapPos) && SameLine(r, swapPos)) pos = swapPos;
                else if (involved.Contains(otherSwapPos) && SameLine(r, otherSwapPos)) pos = otherSwapPos;
                pos = PickSpawnInRun(r, pos, claimed);
                if (pos.x < 0) continue;
                claimed.Add(pos);
                var kind = r.horiz ? SpecialKind.RocketCol : SpecialKind.RocketRow;
                plan.spawns.Add(new SpecialSpawn { x = pos.x, y = pos.y, piece = new Piece(r.color, kind) });
            }

            // 4) 被消除的格子中出现 2×2 同色方块 → 螺旋桨（在方块处生成，留场特殊棋子）
            for (int x = 0; x < GameConfig.BoardCols - 1; x++)
                for (int y = 0; y < GameConfig.BoardRows - 1; y++)
                {
                    var c00 = new Vector2Int(x, y); var c10 = new Vector2Int(x + 1, y);
                    var c01 = new Vector2Int(x, y + 1); var c11 = new Vector2Int(x + 1, y + 1);
                    // 四格必须全部在本次消除范围内：2×2 被消除才生成螺旋桨
                    if (!involved.Contains(c00) || !involved.Contains(c10) ||
                        !involved.Contains(c01) || !involved.Contains(c11)) continue;
                    var p0 = grid[x, y]; var p1 = grid[x + 1, y];
                    var p2 = grid[x, y + 1]; var p3 = grid[x + 1, y + 1];
                    if (p0 == null || p1 == null || p2 == null || p3 == null) continue;
                    if (!p0.HasColor || p1.color != p0.color || p2.color != p0.color || p3.color != p0.color) continue;
                    // 生成位置优先取交换落点，其次方块原点；已被更高优先级特殊棋子占用则退让/放弃
                    Vector2Int spawnPos = c00;
                    if (swapPos == c00 || swapPos == c10 || swapPos == c01 || swapPos == c11) spawnPos = swapPos;
                    if (claimed.Contains(spawnPos)) spawnPos = c00;
                    if (claimed.Contains(spawnPos)) continue;
                    claimed.Add(spawnPos);
                    plan.spawns.Add(new SpecialSpawn { x = spawnPos.x, y = spawnPos.y, piece = new Piece(p0.color, SpecialKind.Propeller) });
                }

            // 汇总待清除格子 = 所有涉及格
            foreach (var c in involved) plan.cells.Add(c);

            // 连锁引爆：波及到的既有特殊棋子按规则扩散
            ExpandChain(plan);
            return plan;
        }

        private bool SameLine(Run r, Vector2Int p)
        {
            if (r.horiz) return p.y == r.y && p.x >= r.x && p.x < r.x + r.len;
            return p.x == r.x && p.y >= r.y && p.y < r.y + r.len;
        }

        /// <summary>
        /// 生成点回退：优先用首选位置；若已被更高优先级特殊棋子占用，回退到该 run 内第一个空闲格。
        /// 全部被占返回 (-1,-1)（放弃生成）。保证低优先级消除不被整体吞掉。
        /// </summary>
        private Vector2Int PickSpawnInRun(Run r, Vector2Int preferred, HashSet<Vector2Int> claimed)
        {
            if (!claimed.Contains(preferred)) return preferred;
            for (int i = 0; i < r.len; i++)
            {
                var c = r.horiz ? new Vector2Int(r.x + i, r.y) : new Vector2Int(r.x, r.y + i);
                if (!claimed.Contains(c)) return c;
            }
            return new Vector2Int(-1, -1);
        }

        /// <summary>螺旋桨目标选择（与关卡目标相关）：未完成目标色棋子 > 障碍格 > 随机棋子。</summary>
        public List<PieceColor> preferredGoalColors = new List<PieceColor>();

        private Vector2Int PickPropellerTarget()
        {
            // 优先未完成的收集目标色
            for (int t = 0; t < 60; t++)
            {
                int x = rng.Next(GameConfig.BoardCols), y = rng.Next(GameConfig.BoardRows);
                var p = grid[x, y];
                if (p != null && p.HasColor && preferredGoalColors.Contains(p.color)) return new Vector2Int(x, y);
            }
            // 其次障碍格（木箱/冰层，命中后 hp-1）
            for (int t = 0; t < 60; t++)
            {
                int x = rng.Next(GameConfig.BoardCols), y = rng.Next(GameConfig.BoardRows);
                if (obstacles[x, y] != null) return new Vector2Int(x, y);
            }
            // 随机棋子
            for (int t = 0; t < 60; t++)
            {
                int x = rng.Next(GameConfig.BoardCols), y = rng.Next(GameConfig.BoardRows);
                if (grid[x, y] != null) return new Vector2Int(x, y);
            }
            return new Vector2Int(rng.Next(GameConfig.BoardCols), rng.Next(GameConfig.BoardRows));
        }

        /// <summary>把 cells 中波及的特殊棋子效果展开（连锁）。</summary>
        private void ExpandChain(ClearPlan plan)
        {
            var queue = new Queue<Vector2Int>(plan.cells);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (!InBounds(c.x, c.y)) continue;
                var p = grid[c.x, c.y];
                if (p == null || !p.IsSpecial) continue;
                bool alreadyPlanned = plan.cells.Contains(c);
                if (!alreadyPlanned) plan.cells.Add(c);

                foreach (var e in DetonationCells(c, p, plan))
                {
                    if (plan.cells.Add(e)) queue.Enqueue(e);
                }
            }
        }

        /// <summary>单个特殊棋子激活时的效果范围（不含自身）。plan 用于螺旋桨对障碍物的直击记录。</summary>
        public List<Vector2Int> DetonationCells(Vector2Int pos, Piece p, ClearPlan plan = null)
        {
            var list = new List<Vector2Int>();
            switch (p.special)
            {
                case SpecialKind.RocketRow:
                    // 横向火箭：消除所在整行
                    for (int x = 0; x < GameConfig.BoardCols; x++) list.Add(new Vector2Int(x, pos.y));
                    break;
                case SpecialKind.RocketCol:
                    // 纵向火箭：消除所在整列
                    for (int y = 0; y < GameConfig.BoardRows; y++) list.Add(new Vector2Int(pos.x, y));
                    break;
                case SpecialKind.Bomb:
                    // 炸弹：自身中心 5×5
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dy = -2; dy <= 2; dy++)
                            if (InBounds(pos.x + dx, pos.y + dy)) list.Add(new Vector2Int(pos.x + dx, pos.y + dy));
                    break;
                case SpecialKind.Propeller:
                {
                    // 螺旋桨：自动消除场上一个目标棋子；目标是障碍时使其 hp-1
                    var t = PickPropellerTarget();
                    if (InBounds(t.x, t.y))
                    {
                        if (HasCrate(t.x, t.y))
                        {
                            // 木箱不受格内清除影响（只被相邻消除波及），这里走直击通道
                            if (plan != null) plan.propellerDirectHits.Add(t);
                        }
                        else
                        {
                            list.Add(t); // 普通棋子格 / 冰层格（冰层在格内被清除时 hp-1）
                        }
                    }
                    break;
                }
                case SpecialKind.Rainbow:
                    // 彩球：消除棋盘上个数最多的一种棋子
                    PieceColor best = MostCommonColor();
                    for (int x = 0; x < GameConfig.BoardCols; x++)
                        for (int y = 0; y < GameConfig.BoardRows; y++)
                        {
                            var q = grid[x, y];
                            if (q != null && q.HasColor && q.color == best) list.Add(new Vector2Int(x, y));
                        }
                    break;
            }
            return list;
        }

        /// <summary>组合技方案（策划案 §3.2.3）。</summary>
        public ClearPlan BuildComboPlan(Vector2Int a, Vector2Int b)
        {
            var pa = grid[a.x, a.y];
            var pb = grid[b.x, b.y];
            var plan = new ClearPlan();

            bool aRain = pa.IsRainbow, bRain = pb.IsRainbow;

            if (aRain && bRain)
            {
                // 彩球+彩球：全盘清除（含障碍 1 层）
                plan.comboName = "彩球+彩球！";
                for (int x = 0; x < GameConfig.BoardCols; x++)
                    for (int y = 0; y < GameConfig.BoardRows; y++)
                        plan.cells.Add(new Vector2Int(x, y));
                ExpandChain(plan);
                return plan;
            }

            if (aRain || bRain)
            {
                Vector2Int rainPos = aRain ? a : b;
                Vector2Int otherPos = aRain ? b : a;
                var other = aRain ? pb : pa;
                if (other.IsSpecial)
                {
                    // 彩球+特殊棋子：场上数量最多的颜色全部转化为对应特殊棋子并触发；
                    // 彩球与原特殊棋子只作为组合材料被清除，自身不另起效果
                    if (IsRocket(other.special)) plan.comboName = "彩虹火箭群！";
                    else if (other.special == SpecialKind.Bomb) plan.comboName = "彩虹炸弹群！";
                    else plan.comboName = "彩虹螺旋桨群！";

                    var targetColor = MostCommonColor();
                    var converted = new List<Vector2Int>();
                    for (int x = 0; x < GameConfig.BoardCols; x++)
                        for (int y = 0; y < GameConfig.BoardRows; y++)
                        {
                            var q = grid[x, y];
                            if (q == null || !q.HasColor || q.color != targetColor) continue;
                            if (x == otherPos.x && y == otherPos.y) continue;
                            // 火箭：每颗随机转为纵向/横向；炸弹/螺旋桨：保持对应种类
                            q.special = IsRocket(other.special)
                                ? (rng.Next(2) == 0 ? SpecialKind.RocketCol : SpecialKind.RocketRow)
                                : other.special;
                            converted.Add(new Vector2Int(x, y));
                        }
                    foreach (var c in converted) plan.cells.Add(c);
                    ExpandChain(plan); // 转化出的特殊棋子在各自位置依次引爆
                    plan.cells.Add(otherPos);
                    plan.cells.Add(rainPos);
                    return plan;
                }
                else
                {
                    // 彩球+普通棋子：清除棋盘上该颜色全部棋子
                    plan.comboName = "彩虹消除！";
                    var targetColor = other.color;
                    for (int x = 0; x < GameConfig.BoardCols; x++)
                        for (int y = 0; y < GameConfig.BoardRows; y++)
                        {
                            var q = grid[x, y];
                            if (q != null && q.HasColor && q.color == targetColor) plan.cells.Add(new Vector2Int(x, y));
                        }
                    ExpandChain(plan);
                    plan.cells.Add(rainPos);
                    return plan;
                }
            }

            // 两个特殊棋子（均非彩虹）
            // 来源两枚先从盘面摘除：作为组合材料被清除，避免 ExpandChain 对其重复引爆
            grid[a.x, a.y] = null;
            grid[b.x, b.y] = null;

            var kindA = pa.special; var kindB = pb.special;
            bool aRocket = IsRocket(kindA);
            bool bRocket = IsRocket(kindB);

            if (aRocket && bRocket)
            {
                // 火箭+火箭：以主动移动棋子的落点 b 为中心消除所在行 + 所在列（与两者朝向无关）
                plan.comboName = "火箭+火箭！十字引爆！";
                for (int x = 0; x < GameConfig.BoardCols; x++) plan.cells.Add(new Vector2Int(x, b.y));
                for (int y = 0; y < GameConfig.BoardRows; y++) plan.cells.Add(new Vector2Int(b.x, y));
            }
            else if (aRocket || bRocket)
            {
                // 主动移动的棋子 pa 落点为 b，pb 落点为 a
                var rocket = aRocket ? pa : pb;
                var rocketPos = aRocket ? b : a;
                var otherKind = aRocket ? kindB : kindA;

                if (otherKind == SpecialKind.Propeller)
                {
                    // 火箭+螺旋桨：无组合特效，火箭在其落点触发 + 螺旋桨触发一次
                    plan.cells.Add(rocketPos);
                    foreach (var e in DetonationCells(rocketPos, rocket, plan))
                        if (InBounds(e.x, e.y)) plan.cells.Add(e);
                    AddPropellerHit(plan);
                }
                else
                {
                    // 火箭+炸弹：以火箭落点为中心，纵向火箭清 3 列 / 横向火箭清 3 行
                    if (rocket.special == SpecialKind.RocketCol)
                    {
                        plan.comboName = "火箭+炸弹！三列贯穿！";
                        for (int dx = -1; dx <= 1; dx++)
                            for (int y = 0; y < GameConfig.BoardRows; y++)
                                if (InBounds(rocketPos.x + dx, y)) plan.cells.Add(new Vector2Int(rocketPos.x + dx, y));
                    }
                    else
                    {
                        plan.comboName = "火箭+炸弹！三行横扫！";
                        for (int dy = -1; dy <= 1; dy++)
                            for (int x = 0; x < GameConfig.BoardCols; x++)
                                if (InBounds(x, rocketPos.y + dy)) plan.cells.Add(new Vector2Int(x, rocketPos.y + dy));
                    }
                }
            }
            else if (kindA == SpecialKind.Bomb && kindB == SpecialKind.Bomb)
            {
                // 炸弹+炸弹：以主动移动棋子的落点 b 为中心的 9×9 范围
                plan.comboName = "炸弹+炸弹！9×9 巨爆！";
                for (int dx = -4; dx <= 4; dx++)
                    for (int dy = -4; dy <= 4; dy++)
                        if (InBounds(b.x + dx, b.y + dy)) plan.cells.Add(new Vector2Int(b.x + dx, b.y + dy));
            }
            else if (kindA == SpecialKind.Propeller && kindB == SpecialKind.Propeller)
            {
                // 螺旋桨+螺旋桨：同时触发 3 个螺旋桨
                plan.comboName = "螺旋桨×3！";
                AddPropellerHit(plan);
                AddPropellerHit(plan);
                AddPropellerHit(plan);
            }
            else
            {
                // 螺旋桨+炸弹：炸弹移动到随机一个目标棋子处引爆（无目标棋子则移动到随机棋子处）
                plan.comboName = "螺旋桨运载炸弹！";
                var t = PickComboBombTarget();
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                        if (InBounds(t.x + dx, t.y + dy)) plan.cells.Add(new Vector2Int(t.x + dx, t.y + dy));
            }

            ExpandChain(plan);
            plan.cells.Add(a);
            plan.cells.Add(b);
            return plan;
        }

        private static bool IsRocket(SpecialKind k)
        {
            return k == SpecialKind.RocketRow || k == SpecialKind.RocketCol;
        }

        /// <summary>场上数量最多的棋子颜色（平局取先遍历者）。</summary>
        private PieceColor MostCommonColor()
        {
            var counts = new Dictionary<PieceColor, int>();
            for (int x = 0; x < GameConfig.BoardCols; x++)
                for (int y = 0; y < GameConfig.BoardRows; y++)
                {
                    var q = grid[x, y];
                    if (q != null && q.HasColor)
                    {
                        if (!counts.ContainsKey(q.color)) counts[q.color] = 0;
                        counts[q.color]++;
                    }
                }
            PieceColor best = PieceColor.Rose; int bestN = -1;
            foreach (var kv in counts)
                if (kv.Value > bestN) { bestN = kv.Value; best = kv.Key; }
            return best;
        }

        /// <summary>螺旋桨触发一次：目标格加入方案（木箱走直击通道，冰层/棋子走格内清除）。</summary>
        private void AddPropellerHit(ClearPlan plan)
        {
            var t = PickPropellerTarget();
            if (!InBounds(t.x, t.y)) return;
            if (HasCrate(t.x, t.y)) plan.propellerDirectHits.Add(t);
            else plan.cells.Add(t);
        }

        /// <summary>螺旋桨+炸弹的炸弹落点：优先随机一个关卡目标棋子，否则随机任意棋子。</summary>
        private Vector2Int PickComboBombTarget()
        {
            var goalCells = new List<Vector2Int>();
            var anyCells = new List<Vector2Int>();
            for (int x = 0; x < GameConfig.BoardCols; x++)
                for (int y = 0; y < GameConfig.BoardRows; y++)
                {
                    var p = grid[x, y];
                    if (p == null) continue;
                    anyCells.Add(new Vector2Int(x, y));
                    if (p.HasColor && preferredGoalColors.Contains(p.color)) goalCells.Add(new Vector2Int(x, y));
                }
            var pool = goalCells.Count > 0 ? goalCells : anyCells;
            if (pool.Count == 0) return new Vector2Int(rng.Next(GameConfig.BoardCols), rng.Next(GameConfig.BoardRows));
            return pool[rng.Next(pool.Count)];
        }

        /// <summary>结算一次消除方案（移除棋子、伤害障碍、放置新特殊棋子）。</summary>
        public ClearResult ApplyPlan(ClearPlan plan)
        {
            var result = new ClearResult();

            foreach (var c in plan.cells)
            {
                if (!InBounds(c.x, c.y)) continue;
                var p = grid[c.x, c.y];
                if (p != null)
                {
                    if (p.HasColor)
                    {
                        if (!result.collected.ContainsKey(p.color)) result.collected[p.color] = 0;
                        result.collected[p.color]++;
                        result.totalPieces++;
                    }
                    grid[c.x, c.y] = null;
                }
                // 冰层：格内消除 -1 层
                var ob = obstacles[c.x, c.y];
                if (ob != null && ob.kind == "ice")
                {
                    ob.hp--;
                    if (ob.hp <= 0) { obstacles[c.x, c.y] = null; result.icesDestroyed++; }
                }
            }

            // 木箱：相邻消除 -1 层
            var crateHits = new HashSet<Vector2Int>();
            foreach (var c in plan.cells)
            {
                Vector2Int[] dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (HasCrate(n.x, n.y) && !crateHits.Contains(n)) crateHits.Add(n);
                }
            }
            foreach (var c in crateHits)
            {
                var ob = obstacles[c.x, c.y];
                ob.hp--;
                if (ob.hp <= 0) { obstacles[c.x, c.y] = null; result.cratesDestroyed++; }
            }

            // 螺旋桨障碍直击：木箱被螺旋桨命中时 hp-1（冰层走格内清除通道，无需在此处理）
            foreach (var h in plan.propellerDirectHits)
            {
                if (!InBounds(h.x, h.y)) continue;
                var ob = obstacles[h.x, h.y];
                if (ob == null) continue;
                ob.hp--;
                if (ob.hp <= 0)
                {
                    if (ob.kind == "crate") result.cratesDestroyed++;
                    else result.icesDestroyed++;
                    obstacles[h.x, h.y] = null;
                }
            }

            // 放置新特殊棋子
            foreach (var sp in plan.spawns)
            {
                if (!InBounds(sp.x, sp.y)) continue;
                grid[sp.x, sp.y] = sp.piece;
            }
            return result;
        }

        // ---------------------------------------------------------------- 重力与补充

        /// <summary>计算并应用一次重力下落 + 顶部补充。</summary>
        public List<GravityMove> ApplyGravity()
        {
            var moves = new List<GravityMove>();
            for (int x = 0; x < GameConfig.BoardCols; x++)
            {
                // 以木箱为界切段；段内自底向上压实；接触顶部的段才补充
                int y = 0;
                while (y < GameConfig.BoardRows)
                {
                    if (HasCrate(x, y)) { y++; continue; }
                    int segBottom = y;
                    int segTop = y;
                    while (segTop < GameConfig.BoardRows && !HasCrate(x, segTop)) segTop++;
                    segTop--; // 段 = [segBottom, segTop]

                    bool touchesTop = segTop == GameConfig.BoardRows - 1;
                    int writeY = segBottom;
                    for (int yy = segBottom; yy <= segTop; yy++)
                    {
                        var p = grid[x, yy];
                        if (p != null)
                        {
                            if (writeY != yy)
                            {
                                grid[x, writeY] = p;
                                grid[x, yy] = null;
                                moves.Add(new GravityMove { col = x, fromRow = yy, toRow = writeY, piece = p });
                            }
                            writeY++;
                        }
                    }
                    if (touchesTop)
                    {
                        for (int yy = writeY; yy <= segTop; yy++)
                        {
                            var np = new Piece(PoolColor());
                            grid[x, yy] = np;
                            moves.Add(new GravityMove { col = x, fromRow = -1, toRow = yy, piece = np });
                        }
                    }
                    y = segTop + 1;
                }
            }
            return moves;
        }

        // ---------------------------------------------------------------- 可用移动与洗牌

        /// <summary>是否存在至少一个合法交换。</summary>
        public bool HasValidMove()
        {
            for (int x = 0; x < GameConfig.BoardCols; x++)
                for (int y = 0; y < GameConfig.BoardRows; y++)
                {
                    var p = grid[x, y];
                    if (p == null) continue;
                    Vector2Int[] dirs = { new Vector2Int(1, 0), new Vector2Int(0, 1) };
                    foreach (var d in dirs)
                    {
                        int nx = x + d.x, ny = y + d.y;
                        if (!InBounds(nx, ny)) continue;
                        var q = grid[nx, ny];
                        if (q == null) continue;
                        if (p.IsSpecial || q.IsSpecial)
                        {
                            // 特殊棋子参与交换可激活；对应触发通道全关时不再视为可用走法（防死局误判/漏洗牌）
                            if (p.IsSpecial && q.IsSpecial)
                            {
                                var rp = rules.Get(p.special);
                                var rq = rules.Get(q.special);
                                if ((rp.swapSpecials && rq.swapSpecials) || rp.clickActivate || rq.clickActivate)
                                    return true;
                            }
                            else
                            {
                                var rs = rules.Get(p.IsSpecial ? p.special : q.special);
                                if (rs.swapWithNormal || rs.clickActivate) return true;
                            }
                        }
                        Swap(x, y, nx, ny);
                        bool ok = HasMatch();
                        if (!ok)
                        {
                            // 主动构造 2×2 同色方块也算可用移动（无三连时所有 2×2 均为独立）
                            Vector2Int[] quad; PieceColor qc;
                            var emptyRuns = new List<Vector2Int>();
                            if (TryGetIndependentQuad(new Vector2Int(nx, ny), emptyRuns, out quad, out qc) ||
                                TryGetIndependentQuad(new Vector2Int(x, y), emptyRuns, out quad, out qc)) ok = true;
                        }
                        Swap(x, y, nx, ny);
                        if (ok) return true;
                    }
                }
            return false;
        }

        /// <summary>自动洗牌（不耗步数）；返回是否成功。</summary>
        public bool Shuffle()
        {
            if (shuffleCount >= GameConfig.ShuffleLimit) return false;
            shuffleCount++;
            var pieces = new List<Piece>();
            var cells = new List<Vector2Int>();
            for (int x = 0; x < GameConfig.BoardCols; x++)
                for (int y = 0; y < GameConfig.BoardRows; y++)
                {
                    if (grid[x, y] != null && grid[x, y].HasColor)
                    {
                        pieces.Add(grid[x, y]);
                        cells.Add(new Vector2Int(x, y));
                        grid[x, y] = null;
                    }
                }
            for (int attempt = 0; attempt < 80; attempt++)
            {
                // Fisher-Yates
                for (int i = pieces.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var t = pieces[i]; pieces[i] = pieces[j]; pieces[j] = t;
                }
                for (int i = 0; i < cells.Count; i++) grid[cells[i].x, cells[i].y] = pieces[i];
                if (!HasMatch() && HasValidMove()) return true;
            }
            // 兜底：直接重新随机颜色
            foreach (var c in cells) grid[c.x, c.y] = new Piece(RandomColorNoMatch(c.x, c.y));
            return true;
        }

        // ---------------------------------------------------------------- 结算辅助

        /// <summary>胜利奖励演出：在随机棋子位置生成一个随机特殊棋子（不放置，直接作为引爆点返回）。</summary>
        public SpecialSpawn MakeBonusSpecialAt(int x, int y)
        {
            var kinds = new[] { SpecialKind.RocketRow, SpecialKind.RocketCol, SpecialKind.Bomb, SpecialKind.Propeller };
            var kind = kinds[rng.Next(kinds.Length)];
            var color = grid[x, y] != null ? grid[x, y].color : PieceColor.Rose;
            grid[x, y] = new Piece(color, kind);
            return new SpecialSpawn { x = x, y = y, piece = grid[x, y] };
        }

        public List<Vector2Int> AllPieceCells()
        {
            var list = new List<Vector2Int>();
            for (int x = 0; x < GameConfig.BoardCols; x++)
                for (int y = 0; y < GameConfig.BoardRows; y++)
                    if (grid[x, y] != null) list.Add(new Vector2Int(x, y));
            return list;
        }

        /// <summary>在 (x,y) 处直接引爆该格特殊棋子的方案（用于胜利奖励/关卡内道具）。</summary>
        public ClearPlan DetonateAt(int x, int y)
        {
            var plan = new ClearPlan();
            var p = grid[x, y];
            if (p == null) return plan;
            plan.cells.Add(new Vector2Int(x, y));
            foreach (var e in DetonationCells(new Vector2Int(x, y), p, plan))
                if (InBounds(e.x, e.y)) plan.cells.Add(e);
            ExpandChain(plan);
            return plan;
        }
    }
}
