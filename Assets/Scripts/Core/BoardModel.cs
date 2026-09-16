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
        public Vector2Int propellerTarget; // 螺旋桨起飞目标（仅 Propeller 用）
        public bool propellerFlies;        // true=生成后立即飞走（不留场）
    }

    /// <summary>一次消除的完整方案。</summary>
    public class ClearPlan
    {
        public HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        public List<SpecialSpawn> spawns = new List<SpecialSpawn>();
        public string comboName; // 组合技提示（如“火箭+炸弹”）
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

    /// <summary>7×9 三消棋盘纯数据模型（不含表现与协程）。</summary>
    public class BoardModel
    {
        public Piece[,] grid;        // [col, row]，row 0 = 底部
        public Obstacle[,] obstacles;
        public System.Random rng;
        public int shuffleCount;

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

        private PieceColor RandomColorNoMatch(int x, int y)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var c = (PieceColor)rng.Next(6);
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

            // 组合技：任一为彩虹球，或两个都是特殊棋子
            if (pa.IsRainbow || pb.IsRainbow || (pa.IsSpecial && pb.IsSpecial))
                return BuildComboPlan(a, b);

            Swap(a.x, a.y, b.x, b.y);
            var runs = FindRuns();
            if (runs.Count == 0)
            {
                // 特殊棋子 + 普通棋子且未构成消除：直接在落点引爆特殊棋子（ Royal Match 规则）
                if (pa.IsSpecial) return PlanSingleDetonation(b, pa);
                if (pb.IsSpecial) return PlanSingleDetonation(a, pb);
                Swap(a.x, a.y, b.x, b.y); // 回弹
                return null;
            }
            var plan = PlanFromRuns(runs, b, a);
            return plan;
        }

        /// <summary>单个特殊棋子在 pos 处引爆的方案（用于交换激活）。</summary>
        private ClearPlan PlanSingleDetonation(Vector2Int pos, Piece p)
        {
            var plan = new ClearPlan();
            switch (p.special)
            {
                case SpecialKind.RocketRow:
                case SpecialKind.RocketCol: plan.comboName = "火箭发射！"; break;
                case SpecialKind.Bomb: plan.comboName = "炸弹引爆！"; break;
                case SpecialKind.Propeller: plan.comboName = "螺旋桨起飞！"; break;
            }
            plan.cells.Add(pos);
            foreach (var e in DetonationCells(pos, p))
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
                if (claimed.Contains(pos)) continue;
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

            // 3) 4 连 → 火箭（横向消除的行火箭 / 纵向消除的列火箭）
            for (int ri = 0; ri < runs.Count; ri++)
            {
                var r = runs[ri];
                if (r.len != 4 || consumed.Contains(ri)) continue;
                Vector2Int pos = new Vector2Int(r.x + 1, r.y);
                if (!r.horiz) pos = new Vector2Int(r.x, r.y + 1);
                if (involved.Contains(swapPos) && SameLine(r, swapPos)) pos = swapPos;
                else if (involved.Contains(otherSwapPos) && SameLine(r, otherSwapPos)) pos = otherSwapPos;
                if (claimed.Contains(pos)) continue;
                claimed.Add(pos);
                var kind = r.horiz ? SpecialKind.RocketRow : SpecialKind.RocketCol;
                plan.spawns.Add(new SpecialSpawn { x = pos.x, y = pos.y, piece = new Piece(r.color, kind) });
            }

            // 4) 2×2 方形 → 螺旋桨（起飞消除目标十字）
            for (int x = 0; x < GameConfig.BoardCols - 1; x++)
                for (int y = 0; y < GameConfig.BoardRows - 1; y++)
                {
                    var p0 = grid[x, y]; var p1 = grid[x + 1, y];
                    var p2 = grid[x, y + 1]; var p3 = grid[x + 1, y + 1];
                    if (p0 == null || p1 == null || p2 == null || p3 == null) continue;
                    if (!p0.HasColor || p1.color != p0.color || p2.color != p0.color || p3.color != p0.color) continue;
                    var c00 = new Vector2Int(x, y); var c10 = new Vector2Int(x + 1, y);
                    var c01 = new Vector2Int(x, y + 1); var c11 = new Vector2Int(x + 1, y + 1);
                    // 仅独立 2×2（不属于任何 run）才触发螺旋桨
                    if (involved.Contains(c00) || involved.Contains(c10) ||
                        involved.Contains(c01) || involved.Contains(c11)) continue;
                    if (claimed.Contains(c00) || claimed.Contains(c10) || claimed.Contains(c01) || claimed.Contains(c11)) continue;
                    involved.Add(c00); involved.Add(c10); involved.Add(c01); involved.Add(c11);
                    Vector2Int spawnPos = c00;
                    if (swapPos == c00 || swapPos == c10 || swapPos == c01 || swapPos == c11) spawnPos = swapPos;
                    claimed.Add(spawnPos);
                    var sp = new SpecialSpawn
                    {
                        x = spawnPos.x,
                        y = spawnPos.y,
                        piece = new Piece(p0.color, SpecialKind.Propeller),
                        propellerFlies = true,
                        propellerTarget = PickPropellerTarget(p0.color)
                    };
                    plan.spawns.Add(sp);
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

        /// <summary>螺旋桨目标选择：未完成目标色 > 障碍 > 随机棋子（由 controller 注入目标色集合）。</summary>
        public List<PieceColor> preferredGoalColors = new List<PieceColor>();

        private Vector2Int PickPropellerTarget(PieceColor spawnColor)
        {
            // 优先目标色
            for (int t = 0; t < 30; t++)
            {
                int x = rng.Next(GameConfig.BoardCols), y = rng.Next(GameConfig.BoardRows);
                var p = grid[x, y];
                if (p != null && p.HasColor && preferredGoalColors.Contains(p.color)) return new Vector2Int(x, y);
            }
            // 其次障碍格
            for (int t = 0; t < 30; t++)
            {
                int x = rng.Next(GameConfig.BoardCols), y = rng.Next(GameConfig.BoardRows);
                if (obstacles[x, y] != null) return new Vector2Int(x, y);
            }
            // 随机棋子
            for (int t = 0; t < 30; t++)
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

                foreach (var e in DetonationCells(c, p))
                {
                    if (plan.cells.Add(e)) queue.Enqueue(e);
                }
            }
        }

        /// <summary>单个特殊棋子引爆时的效果范围（不含自身）。</summary>
        public List<Vector2Int> DetonationCells(Vector2Int pos, Piece p)
        {
            var list = new List<Vector2Int>();
            switch (p.special)
            {
                case SpecialKind.RocketRow:
                    for (int x = 0; x < GameConfig.BoardCols; x++) list.Add(new Vector2Int(x, pos.y));
                    break;
                case SpecialKind.RocketCol:
                    for (int y = 0; y < GameConfig.BoardRows; y++) list.Add(new Vector2Int(pos.x, y));
                    break;
                case SpecialKind.Bomb:
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if (InBounds(pos.x + dx, pos.y + dy)) list.Add(new Vector2Int(pos.x + dx, pos.y + dy));
                    break;
                case SpecialKind.Propeller:
                    list.Add(pos + new Vector2Int(0, 1));
                    list.Add(pos + new Vector2Int(0, -1));
                    list.Add(pos + new Vector2Int(1, 0));
                    list.Add(pos + new Vector2Int(-1, 0));
                    break;
                case SpecialKind.Rainbow:
                    // 被爆炸波及的彩球：清除场上数量最多的一种颜色
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
                    // 彩球+特殊棋子：该颜色全部棋子转化为对应特殊棋子后依次引爆
                    // （彩球位置最后再加入，避免 ExpandChain 把彩球本身当引爆点多炸一种颜色）
                    plan.comboName = "彩虹转化！";
                    var targetColor = other.color;
                    var converted = new List<Vector2Int>();
                    for (int x = 0; x < GameConfig.BoardCols; x++)
                        for (int y = 0; y < GameConfig.BoardRows; y++)
                        {
                            var q = grid[x, y];
                            if (q != null && q.HasColor && q.color == targetColor && !(x == otherPos.x && y == otherPos.y))
                            {
                                q.special = other.special;
                                converted.Add(new Vector2Int(x, y));
                            }
                        }
                    plan.cells.Add(otherPos);
                    foreach (var c in converted) plan.cells.Add(c);
                    ExpandChain(plan);
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
            plan.cells.Add(a);
            plan.cells.Add(b);
            Vector2Int center = b; // 以交换落点为中心
            var kindA = pa.special; var kindB = pb.special;
            bool aRocket = kindA == SpecialKind.RocketRow || kindA == SpecialKind.RocketCol;
            bool bRocket = kindB == SpecialKind.RocketRow || kindB == SpecialKind.RocketCol;

            if (aRocket && bRocket)
            {
                plan.comboName = "火箭+火箭！";
                for (int x = 0; x < GameConfig.BoardCols; x++) plan.cells.Add(new Vector2Int(x, center.y));
                for (int y = 0; y < GameConfig.BoardRows; y++) plan.cells.Add(new Vector2Int(center.x, y));
            }
            else if ((aRocket && kindB == SpecialKind.Bomb) || (bRocket && kindA == SpecialKind.Bomb))
            {
                // 火箭+炸弹：横向火箭清 3 行，纵向火箭清 3 列
                var rocketKind = aRocket ? kindA : kindB;
                if (rocketKind == SpecialKind.RocketRow)
                {
                    plan.comboName = "火箭+炸弹！三行横扫！";
                    for (int dy = -1; dy <= 1; dy++)
                        for (int x = 0; x < GameConfig.BoardCols; x++)
                            if (InBounds(x, center.y + dy)) plan.cells.Add(new Vector2Int(x, center.y + dy));
                }
                else
                {
                    plan.comboName = "火箭+炸弹！三列贯穿！";
                    for (int dx = -1; dx <= 1; dx++)
                        for (int y = 0; y < GameConfig.BoardRows; y++)
                            if (InBounds(center.x + dx, y)) plan.cells.Add(new Vector2Int(center.x + dx, y));
                }
            }
            else if (kindA == SpecialKind.Bomb && kindB == SpecialKind.Bomb)
            {
                plan.comboName = "炸弹+炸弹！";
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                        if (InBounds(center.x + dx, center.y + dy)) plan.cells.Add(new Vector2Int(center.x + dx, center.y + dy));
            }
            else
            {
                // 其他组合（螺旋桨参与）：按两个特殊棋子各自引爆处理
                plan.comboName = "组合引爆！";
                foreach (var e in DetonationCells(a, pa)) plan.cells.Add(e);
                foreach (var e in DetonationCells(b, pb)) plan.cells.Add(e);
            }
            ExpandChain(plan);
            return plan;
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

            // 放置新特殊棋子
            foreach (var sp in plan.spawns)
            {
                if (!InBounds(sp.x, sp.y)) continue;
                if (sp.propellerFlies) continue; // 螺旋桨起飞，不留场
                grid[sp.x, sp.y] = sp.piece;
            }
            return result;
        }

        /// <summary>螺旋桨起飞目标格也需清除（由 plan 构建时加入）。</summary>
        public void AddPropellerTargetToPlan(ClearPlan plan)
        {
            foreach (var sp in plan.spawns)
            {
                if (!sp.propellerFlies) continue;
                var t = sp.propellerTarget;
                if (!InBounds(t.x, t.y)) continue;
                plan.cells.Add(t);
                var p = grid[t.x, t.y];
                if (p != null && p.IsSpecial)
                {
                    foreach (var e in DetonationCells(t, p))
                        if (InBounds(e.x, e.y)) plan.cells.Add(e);
                }
            }
            ExpandChain(plan);
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
                            var np = new Piece((PieceColor)rng.Next(6));
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
                        if (p.IsSpecial || q.IsSpecial) return true; // 特殊棋子参与交换总可激活
                        Swap(x, y, nx, ny);
                        bool ok = HasMatch();
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
            return new SpecialSpawn { x = x, y = y, piece = grid[x, y], propellerFlies = false };
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
            foreach (var e in DetonationCells(new Vector2Int(x, y), p))
                if (InBounds(e.x, e.y)) plan.cells.Add(e);
            ExpandChain(plan);
            return plan;
        }
    }
}
