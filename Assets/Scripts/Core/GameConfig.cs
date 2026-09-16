namespace StarManor
{
    /// <summary>全局配置常量（策划案 §4.3 level_config 核心字段对应）。</summary>
    public static class GameConfig
    {
        public const int BoardCols = 7;
        public const int BoardRows = 9;

        public const int MaxLives = 5;
        public const int LifeRegenMinutes = 30;

        public const int ContinueCostGold = 900;   // 续关 +5 步
        public const int ContinueExtraSteps = 5;
        public const int ContinueLimitPerLevel = 2;

        public const int RewardGoldNormal = 120;   // 通关金币（简化：不按章节爬升）
        public const int RewardGoldHard = 240;

        public const int ShuffleLimit = 5;         // 单关自动洗牌上限

        // UI 设计基准分辨率（竖屏 1170x2532）
        public const int DesignWidth = 1170;
        public const int DesignHeight = 2532;

        public const float CellSize = 118f;        // 棋盘格子像素尺寸
        public const float CellGap = 4f;
    }

    /// <summary>基础棋子 6 色（红花/蓝滴/绿叶/黄星/紫晶/橙果）。</summary>
    public enum PieceColor
    {
        Rose = 0,   // 红花
        Drop = 1,   // 蓝滴
        Leaf = 2,   // 绿叶
        Star = 3,   // 黄星
        Gem = 4,    // 紫晶
        Orange = 5, // 橙果
    }

    /// <summary>特殊棋子（策划案 §3.2.2）。</summary>
    public enum SpecialKind
    {
        None = 0,
        RocketRow = 1,  // 火箭·横向（清除整行）
        RocketCol = 2,  // 火箭·纵向（清除整列）
        Bomb = 3,       // 炸弹（3x3）
        Rainbow = 4,    // 彩球（无色）
        Propeller = 5,  // 螺旋桨（十字 5 格）
    }

    public static class PieceColorExt
    {
        public static string Name(this PieceColor c)
        {
            switch (c)
            {
                case PieceColor.Rose: return "rose";
                case PieceColor.Drop: return "drop";
                case PieceColor.Leaf: return "leaf";
                case PieceColor.Star: return "star";
                case PieceColor.Gem: return "gem";
                default: return "orange";
            }
        }

        public static PieceColor FromName(string s)
        {
            switch (s)
            {
                case "rose": return PieceColor.Rose;
                case "drop": return PieceColor.Drop;
                case "leaf": return PieceColor.Leaf;
                case "star": return PieceColor.Star;
                case "gem": return PieceColor.Gem;
                default: return PieceColor.Orange;
            }
        }

        public static string Cn(this PieceColor c)
        {
            switch (c)
            {
                case PieceColor.Rose: return "红花";
                case PieceColor.Drop: return "蓝滴";
                case PieceColor.Leaf: return "绿叶";
                case PieceColor.Star: return "黄星";
                case PieceColor.Gem: return "紫晶";
                default: return "橙果";
            }
        }
    }
}
