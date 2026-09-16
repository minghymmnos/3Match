using System.Collections.Generic;
using UnityEngine;

namespace StarManor
{
    /// <summary>
    /// 素材加载（资源由工具管线预清理为独立 PNG，见 .workbuddy/tools/clean_assets.py）。
    /// 背景沿用原图（无棋盘格底）；其余一律加载 Clean/ 下已去底、去噪、去水印的单帧图。
    /// </summary>
    public static class SpriteLib
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        private static Texture2D Tex(string path)
        {
            var t = Resources.Load<Texture2D>(path);
            if (t == null) Debug.LogError("[SpriteLib] 找不到贴图: " + path);
            return t;
        }

        private static Sprite Full(string path)
        {
            Sprite s;
            if (_cache.TryGetValue(path, out s) && s != null) return s;
            var t = Tex(path);
            if (t == null) return null;
            s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _cache[path] = s;
            return s;
        }

        /// <summary>整图 Sprite 并带九宫格边框（L,B,R,T），供 Sliced 拉伸使用。</summary>
        private static Sprite FullSliced(string path, Vector4 border)
        {
            string key = path + "#sliced";
            Sprite s;
            if (_cache.TryGetValue(key, out s) && s != null) return s;
            var t = Tex(path);
            if (t == null) return null;
            s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            _cache[key] = s;
            return s;
        }

        // ---------------- 棋子 ----------------

        public static Sprite Piece(PieceColor c) { return Full("Clean/Pieces/blk_" + c.Name() + "_idle_01"); }

        public static Sprite Special(SpecialKind k)
        {
            switch (k)
            {
                case SpecialKind.RocketRow:
                case SpecialKind.RocketCol: return Full("Clean/Pieces/sp_rocket_idle_01");
                case SpecialKind.Bomb: return Full("Clean/Pieces/sp_bomb_idle_01");
                case SpecialKind.Rainbow: return Full("Clean/Pieces/sp_rainbowball_idle_01");
                case SpecialKind.Propeller: return Full("Clean/Pieces/sp_propeller_idle_01");
                default: return null;
            }
        }

        public static Sprite PieceFor(Piece p)
        {
            if (p == null) return null;
            if (p.IsRainbow) return Special(SpecialKind.Rainbow);
            if (p.IsSpecial) return Special(p.special);
            return Piece(p.color);
        }

        // ---------------- 背景（原图，无棋盘格底） ----------------

        public static Sprite BgManor() { return Full("Backgrounds/bg_manor_view"); }
        public static Sprite BgMap() { return Full("Backgrounds/bg_level_map"); }
        public static Sprite BgGarden() { return Full("Backgrounds/bg_garden_full"); }

        // ---------------- 面板 / 棋盘 ----------------

        public static Sprite PanelBase() { return Full("Clean/UI/ui_panel_base_01"); }

        /// <summary>面板九宫格切片：顶部边框含缎带横幅（约 230px），Sliced 拉伸时缎带只横向伸展、中部纯色区自由伸缩。</summary>
        public static Sprite PanelSliced() { return FullSliced("Clean/UI/ui_panel_base_01", new Vector4(90, 90, 90, 230)); }
        public static Sprite BoardBase() { return Full("Clean/UI/ui_board_base"); }
        public static Sprite BoardCellLight() { return Full("Clean/UI/cell_light"); }
        public static Sprite BoardCellDark() { return Full("Clean/UI/cell_dark"); }

        // ---------------- 按钮三态（九宫格：圆角约 110x80） ----------------

        public static Sprite BtnNormal() { return FullSliced("Clean/Buttons/btn_normal", new Vector4(110, 80, 110, 80)); }
        public static Sprite BtnPressed() { return FullSliced("Clean/Buttons/btn_pressed", new Vector4(110, 80, 110, 80)); }
        public static Sprite BtnDisabled() { return FullSliced("Clean/Buttons/btn_disabled", new Vector4(110, 80, 110, 80)); }

        // ---------------- 货币图标（金币/星星/生命/代币/齿轮） ----------------

        public static Sprite IconCoin() { return Full("Clean/Icons/icon_coin"); }
        public static Sprite IconStar() { return Full("Clean/Icons/icon_star"); }
        public static Sprite IconLife() { return Full("Clean/Icons/icon_heart"); }
        public static Sprite IconToken() { return Full("Clean/Icons/icon_ticket"); }
        public static Sprite IconGear() { return Full("Clean/Icons/icon_gear"); }

        // ---------------- 关卡节点（完成/当前/锁定/困难/超难） ----------------

        public static Sprite LevelNode(int index)
        {
            switch (index)
            {
                case 0: return Full("Clean/Nodes/node_done");
                case 1: return Full("Clean/Nodes/node_current");
                case 2: return Full("Clean/Nodes/node_locked");
                case 3: return Full("Clean/Nodes/node_hard");
                default: return Full("Clean/Nodes/node_super");
            }
        }

        // ---------------- 进度条 ----------------

        public static Sprite BarEmpty() { return FullSliced("Clean/Bars/bar_empty", new Vector4(60, 50, 60, 50)); }
        public static Sprite BarFillTeal() { return Full("Clean/Bars/bar_fill_teal"); }
        public static Sprite BarFillGold() { return Full("Clean/Bars/bar_fill_gold"); }

        // ---------------- 特效 ----------------

        public static Sprite Burst(PieceColor c) { return Full("Clean/Effects/fx_burst_" + c.Name()); }
        public static Sprite FxFlash() { return Full("Clean/Effects/fx_explosion_flash_01"); }
        public static Sprite FxRainbowWave() { return Full("Clean/Effects/fx_rainbow_wave"); }
        public static Sprite FxSparkle() { return Full("Clean/Effects/fx_sparkle_trail"); }
    }
}
