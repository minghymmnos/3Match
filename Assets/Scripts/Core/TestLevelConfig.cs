using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarManor
{
    [Serializable]
    public class TestGoalSetting
    {
        public PieceColor color = PieceColor.Rose; // 目标棋子颜色
        public int count = 10;                     // 需要消除的个数
    }

    /// <summary>
    /// 测试关卡配置（挂载到 Gameplay 场景内任意物体上，进入测试关时自动读取）：
    /// 1. 目标：可配置多条"收集指定颜色棋子"目标（留空 = 无目标）
    /// 2. 步数：勾选无限步（默认），或指定有限步数走完整胜/负流程
    /// 3. 棋子颜色池：只保留部分颜色，更容易凑出四连/五连/2×2 等特殊棋子
    /// 修改参数后通过 暂停 → 重新开始 生效。正式关卡不受本组件影响。
    /// </summary>
    public class TestLevelConfig : MonoBehaviour
    {
        /// <summary>跨场景单例：挂在主菜单或 Gameplay 场景均可，切场景自动保留。</summary>
        public static TestLevelConfig Instance { get; private set; }

        [Header("目标（收集指定颜色棋子，可添加多条；留空则无目标）")]
        public List<TestGoalSetting> goals = new List<TestGoalSetting>();

        [Header("步数：勾选则无限步（显示 ∞，不结算胜负）")]
        public bool infiniteSteps = true;

        [Header("有限步数（infiniteSteps 取消勾选时生效）")]
        [Min(1)] public int steps = 30;

        [Header("棋子颜色池（留空 = 6 色全开；只留 2~3 种更容易触发特殊棋子）")]
        public List<PieceColor> allowedColors = new List<PieceColor>();

        [Header("特殊棋子触发开关（仅测试关生效；每类棋子三个通道独立，均默认开启）")]
        [Tooltip("火箭（纵向/横向共用）：单击 / 与普通棋子交换 / 与其他特殊棋子交换")]
        public BoardModel.PieceRule rocketRule = new BoardModel.PieceRule();
        [Tooltip("炸弹：单击 / 与普通棋子交换 / 与其他特殊棋子交换")]
        public BoardModel.PieceRule bombRule = new BoardModel.PieceRule();
        [Tooltip("螺旋桨：单击 / 与普通棋子交换 / 与其他特殊棋子交换")]
        public BoardModel.PieceRule propellerRule = new BoardModel.PieceRule();
        [Tooltip("彩球：与普通棋子交换 / 与其他特殊棋子交换（彩球不能单击触发，Click Activate 无效）")]
        public BoardModel.PieceRule rainbowRule = new BoardModel.PieceRule();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject); // 主菜单场景挂载时也能带入 Gameplay 生效
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>构建自定义 LevelDef（BoardController 进入测试关时调用）。</summary>
        public LevelDef BuildLevelDef()
        {
            var def = new LevelDef
            {
                id = 999,
                steps = infiniteSteps ? -1 : Mathf.Max(1, steps),
                difficulty = "NORMAL",
                rewardGold = 0,
                test = infiniteSteps, // 无限步沿用测试关通道；有限步走完整胜负流程
                goals = new GoalDef[goals != null ? goals.Count : 0],
            };
            for (int i = 0; i < def.goals.Length; i++)
            {
                var g = goals[i];
                def.goals[i] = new GoalDef
                {
                    type = "collect",
                    color = g.color.Name(),
                    count = Mathf.Max(1, g.count),
                };
            }
            return def;
        }

        /// <summary>去重后的颜色池；为空时返回全部 6 色。</summary>
        public List<PieceColor> BuildColorPool()
        {
            var pool = new List<PieceColor>();
            if (allowedColors != null)
                foreach (var c in allowedColors)
                    if (!pool.Contains(c)) pool.Add(c);
            if (pool.Count == 0)
                pool.AddRange(new[] { PieceColor.Rose, PieceColor.Drop, PieceColor.Leaf,
                                      PieceColor.Star, PieceColor.Gem, PieceColor.Orange });
            return pool;
        }

        /// <summary>构建特殊棋子触发开关（BoardController 进测试关时写入 BoardModel.rules）。</summary>
        public BoardModel.RuleToggles BuildRuleToggles()
        {
            var t = new BoardModel.RuleToggles();
            if (rocketRule != null) t.rocket = CopyRule(rocketRule);
            if (bombRule != null) t.bomb = CopyRule(bombRule);
            if (propellerRule != null) t.propeller = CopyRule(propellerRule);
            if (rainbowRule != null) t.rainbow = CopyRule(rainbowRule);
            return t;
        }

        private static BoardModel.PieceRule CopyRule(BoardModel.PieceRule r)
        {
            return new BoardModel.PieceRule
            {
                clickActivate = r.clickActivate,
                swapWithNormal = r.swapWithNormal,
                swapSpecials = r.swapSpecials,
            };
        }
    }
}
