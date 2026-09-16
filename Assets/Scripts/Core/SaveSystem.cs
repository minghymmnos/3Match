using System;
using UnityEngine;

namespace StarManor
{
    /// <summary>经济与进度存档（PlayerPrefs）：生命（30 分钟/点）、金币、星星、关卡进度。</summary>
    public static class SaveSystem
    {
        private const string KGold = "sm_gold";
        private const string KStars = "sm_stars";
        private const string KMaxLevel = "sm_max_level"; // 已解锁的最大关卡
        private const string KLives = "sm_lives";
        private const string KLivesTs = "sm_lives_ts";   // 上次生命恢复时间戳
        private const string KUnlimitedUntil = "sm_unlimited_until";
        private const string KSound = "sm_sound";
        private const string KContinueUsed = "sm_continue_used"; // 本关已续关次数

        public static event Action OnChanged;

        private static void Notify() { if (OnChanged != null) OnChanged(); }

        // ---------------- 金币 / 星星 ----------------

        public static int Gold
        {
            get { return PlayerPrefs.GetInt(KGold, 500); }
            set { PlayerPrefs.SetInt(KGold, Mathf.Max(0, value)); Notify(); }
        }

        public static void AddGold(int n) { Gold = Gold + n; }

        public static int Stars
        {
            get { return PlayerPrefs.GetInt(KStars, 0); }
            set { PlayerPrefs.SetInt(KStars, Mathf.Max(0, value)); Notify(); }
        }

        public static void AddStars(int n) { Stars = Stars + n; }

        public static int MaxUnlockedLevel
        {
            get { return PlayerPrefs.GetInt(KMaxLevel, 1); }
            set { PlayerPrefs.SetInt(KMaxLevel, Mathf.Max(1, value)); Notify(); }
        }

        // ---------------- 生命 ----------------

        public static bool UnlimitedLife
        {
            get
            {
                long until = Convert.ToInt64(PlayerPrefs.GetString(KUnlimitedUntil, "0"));
                return DateTime.UtcNow.Ticks < until;
            }
        }

        public static string UnlimitedRemainingText()
        {
            long until = Convert.ToInt64(PlayerPrefs.GetString(KUnlimitedUntil, "0"));
            var remain = TimeSpan.FromTicks(until - DateTime.UtcNow.Ticks);
            if (remain.TotalSeconds <= 0) return "";
            return string.Format("{0:00}:{1:00}", remain.TotalHours, remain.Minutes);
        }

        public static void GrantUnlimitedLifeMinutes(int minutes)
        {
            long baseTicks = UnlimitedLife ? Convert.ToInt64(PlayerPrefs.GetString(KUnlimitedUntil, "0")) : DateTime.UtcNow.Ticks;
            var until = baseTicks + TimeSpan.FromMinutes(minutes).Ticks;
            PlayerPrefs.SetString(KUnlimitedUntil, until.ToString());
            Notify();
        }

        public static int Lives
        {
            get
            {
                if (UnlimitedLife) return GameConfig.MaxLives;
                int lives = RegenLives(PlayerPrefs.GetInt(KLives, GameConfig.MaxLives));
                return lives;
            }
        }

        public static string LifeRegenText()
        {
            if (UnlimitedLife) return "∞";
            if (Lives >= GameConfig.MaxLives) return "已满";
            long ts = Convert.ToInt64(PlayerPrefs.GetString(KLivesTs, "0"));
            var elapsed = DateTime.UtcNow.Ticks - ts;
            var remain = TimeSpan.FromMinutes(GameConfig.LifeRegenMinutes) - TimeSpan.FromTicks(elapsed);
            if (remain.Ticks <= 0) return "已满";
            return string.Format("{0:00}:{1:00}", remain.Minutes, remain.Seconds);
        }

        private static int RegenLives(int stored)
        {
            if (stored >= GameConfig.MaxLives) return stored;
            long ts = Convert.ToInt64(PlayerPrefs.GetString(KLivesTs, "0"));
            if (ts <= 0)
            {
                PlayerPrefs.SetString(KLivesTs, DateTime.UtcNow.Ticks.ToString());
                return stored;
            }
            var elapsed = DateTime.UtcNow - new DateTime(ts);
            int regen = (int)(elapsed.TotalMinutes / GameConfig.LifeRegenMinutes);
            if (regen <= 0) return stored;
            int now = Mathf.Min(GameConfig.MaxLives, stored + regen);
            PlayerPrefs.SetInt(KLives, now);
            if (now >= GameConfig.MaxLives)
                PlayerPrefs.SetString(KLivesTs, "0");
            else
                PlayerPrefs.SetString(KLivesTs, DateTime.UtcNow.Ticks.ToString());
            return now;
        }

        /// <summary>消耗 1 点生命（进入关卡时校验）。</summary>
        public static bool TryConsumeLife()
        {
            if (UnlimitedLife) return true;
            int lives = Lives;
            if (lives <= 0) return false;
            lives--;
            PlayerPrefs.SetInt(KLives, lives);
            if (PlayerPrefs.GetString(KLivesTs, "0") == "0")
                PlayerPrefs.SetString(KLivesTs, DateTime.UtcNow.Ticks.ToString());
            Notify();
            return true;
        }

        /// <summary>失败扣 1 点生命（策划案 §6.3）。</summary>
        public static void ConsumeLifeOnFail()
        {
            if (UnlimitedLife) return;
            int lives = Mathf.Max(0, Lives - 1);
            PlayerPrefs.SetInt(KLives, lives);
            Notify();
        }

        public static void GrantLife() { PlayerPrefs.SetInt(KLives, Mathf.Min(GameConfig.MaxLives, Lives + 1)); Notify(); }

        // ---------------- 续关 ----------------

        public static int ContinueUsed
        {
            get { return PlayerPrefs.GetInt(KContinueUsed, 0); }
            set { PlayerPrefs.SetInt(KContinueUsed, value); }
        }

        // ---------------- 设置 ----------------

        public static bool SoundOn
        {
            get { return PlayerPrefs.GetInt(KSound, 1) == 1; }
            set { PlayerPrefs.SetInt(KSound, value ? 1 : 0); Notify(); }
        }

        public static void Save() { PlayerPrefs.Save(); }
    }
}
