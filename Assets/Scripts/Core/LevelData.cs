using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarManor
{
    [Serializable]
    public class GoalDef
    {
        public string type;   // "collect" 收集颜色 | "clear" 清除障碍
        public string color;  // collect 用
        public string target; // clear 用：crate / ice
        public int count;
    }

    [Serializable]
    public class CellDef
    {
        public int x;
        public int y;
        public int hp; // 层数
    }

    [Serializable]
    public class LevelDef
    {
        public int id;
        public int steps;
        public string difficulty; // NORMAL / HARD
        public int rewardGold;
        public GoalDef[] goals;
        public CellDef[] crates; // 木箱
        public CellDef[] ices;   // 冰层
    }

    [Serializable]
    public class LevelList
    {
        public LevelDef[] levels;
    }

    /// <summary>关卡数据库：从 Resources/Levels/levels.json 加载。</summary>
    public static class LevelDatabase
    {
        private static List<LevelDef> _levels;

        public static List<LevelDef> All
        {
            get
            {
                if (_levels == null) Load();
                return _levels;
            }
        }

        public static LevelDef Get(int id)
        {
            var all = All;
            if (all != null)
            {
                foreach (var l in all)
                    if (l.id == id) return l;
            }
            return null;
        }

        public static int Count
        {
            get { return All != null ? All.Count : 0; }
        }

        private static void Load()
        {
            _levels = new List<LevelDef>();
            var asset = Resources.Load<TextAsset>("Levels/levels");
            if (asset == null)
            {
                Debug.LogError("[LevelDatabase] 找不到 Resources/Levels/levels.json");
                return;
            }
            var list = JsonUtility.FromJson<LevelList>(asset.text);
            if (list != null && list.levels != null)
                _levels.AddRange(list.levels);
        }
    }
}
