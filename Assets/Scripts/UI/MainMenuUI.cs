using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarManor
{
    /// <summary>主界面（庄园）：背景 + 顶栏（生命/金币/星星）+ 关卡地图 + 主按钮区。</summary>
    public class MainMenuUI : MonoBehaviour
    {
        private Canvas _canvas;
        private Text _lifeText, _goldText, _starText, _lifeRegenText;
        private Transform _gridRoot;
        private GameObject _modal;

        private void Start()
        {
            _canvas = UIFactory.CreateCanvas("MainMenuCanvas", transform);
            BuildStatic();
            RefreshTopBar();
            BuildLevelGrid();
        }

        private void Update()
        {
            // 生命恢复倒计时每秒刷新
            RefreshTopBar();
        }

        private void BuildStatic()
        {
            // 背景（1536x1024 横版素材，等比 Cover 铺满）
            var bg = UIFactory.MakeImage(_canvas.transform, "BG", SpriteLib.BgManor(), Vector2.zero, Vector2.zero, false, false);
            UIFactory.FitCover(bg.rectTransform, new Vector2(1536, 1024));

            // 标题（锚定顶部，不随屏幕高度漂移）
            var title = UIFactory.Text(_canvas.transform, "Title", "星语庄园", 130, new Color(1f, 0.96f, 0.85f),
                new Vector2(800, 170), new Vector2(0, -250));
            title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            var titleOl = title.gameObject.AddComponent<UnityEngine.UI.Outline>();
            titleOl.effectColor = new Color(0.25f, 0.15f, 0.3f, 0.9f);
            titleOl.effectDistance = new Vector2(2.5f, -2.5f);

            // 顶栏
            var top = UIFactory.Panel(_canvas.transform, "TopBar",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(0, 0),
                new Color(0.13f, 0.09f, 0.2f, 0.55f));

            // 顶栏（图标与数字错开排布：数字框左缘与图标右缘留约 10px 间隙，互不压叠）
            UIFactory.MakeImage(top, "LifeIcon", SpriteLib.IconLife(), new Vector2(80, 80), new Vector2(-545, 4));
            _lifeText = UIFactory.Text(top, "LifeNum", "5/5", 50, Color.white, new Vector2(140, 70), new Vector2(-425, 4), TextAnchor.MiddleLeft);
            var lifeOl = _lifeText.gameObject.AddComponent<UnityEngine.UI.Outline>();
            lifeOl.effectColor = new Color(0.1f, 0.06f, 0.16f, 0.9f);
            lifeOl.effectDistance = new Vector2(1.6f, -1.6f);
            _lifeRegenText = UIFactory.Text(top, "LifeRegen", "", 32, new Color(1f, 1f, 1f, 0.85f), new Vector2(180, 44), new Vector2(-425, -48), TextAnchor.MiddleLeft);

            UIFactory.MakeImage(top, "GoldIcon", SpriteLib.IconCoin(), new Vector2(80, 80), new Vector2(-160, 4));
            _goldText = UIFactory.Text(top, "GoldNum", "0", 50, new Color(1f, 0.92f, 0.55f), new Vector2(170, 70), new Vector2(-30, 4), TextAnchor.MiddleLeft);
            var goldOl = _goldText.gameObject.AddComponent<UnityEngine.UI.Outline>();
            goldOl.effectColor = new Color(0.1f, 0.06f, 0.16f, 0.9f);
            goldOl.effectDistance = new Vector2(1.6f, -1.6f);

            UIFactory.MakeImage(top, "StarIcon", SpriteLib.IconStar(), new Vector2(80, 80), new Vector2(135, 4));
            _starText = UIFactory.Text(top, "StarNum", "0", 50, new Color(1f, 0.92f, 0.55f), new Vector2(170, 70), new Vector2(265, 4), TextAnchor.MiddleLeft);
            var starOl = _starText.gameObject.AddComponent<UnityEngine.UI.Outline>();
            starOl.effectColor = new Color(0.1f, 0.06f, 0.16f, 0.9f);
            starOl.effectDistance = new Vector2(1.6f, -1.6f);

            // 设置按钮（锚定顶栏右缘，按钮完整落在栏内，文字提示在按钮左侧）
            var gear = UIFactory.IconButton(top, SpriteLib.IconGear(), new Vector2(84, 84), Vector2.zero, ShowSettings);
            var gearRt = (RectTransform)gear.transform;
            gearRt.anchorMin = gearRt.anchorMax = new Vector2(1f, 0.5f);
            gearRt.anchoredPosition = new Vector2(-92, 0);
            var hint = UIFactory.Text(top, "SettingsHint", "设置", 28, Color.white, new Vector2(90, 40), Vector2.zero, TextAnchor.MiddleRight);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            hint.rectTransform.anchoredPosition = new Vector2(-148, 0);

            // 关卡地图面板（九宫格面板 + 顶部缎带，替代被裁切变形的横版背景图）
            var map = UIFactory.Panel(_canvas.transform, "LevelMap",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-520, -260), new Vector2(520, 760), new Color(1f, 1f, 1f, 0f));
            var mapBg = UIFactory.MakeImage(map, "MapBG", SpriteLib.PanelSliced(), new Vector2(1040, 1020), Vector2.zero, true, false);
            mapBg.type = Image.Type.Sliced;
            mapBg.color = new Color(1f, 1f, 1f, 0.97f);

            _gridRoot = UIFactory.Anchored(map, "Grid", new Vector2(0.5f, 0.5f), new Vector2(780, 720), new Vector2(0, -95));

            // 地图标题放在缎带横幅内（缎带中心约在面板顶部下方 115px）
            UIFactory.Text(map, "MapTitle", "选择关卡", 58, new Color(0.42f, 0.24f, 0.1f), new Vector2(500, 80), new Vector2(0, 398));

            // 底部主按钮区（锚定底部，不随屏幕高度漂移）
            AnchorBottom(UIFactory.TextButton(_canvas.transform, "▶ 继续关卡", 64, new Vector2(620, 150), new Vector2(0, 190), OnPlayClicked).transform, new Vector2(0, 190));
            AnchorBottom(UIFactory.TextButton(_canvas.transform, "商城", 44, new Vector2(260, 100), new Vector2(-310, 70), ShowShop).transform, new Vector2(-310, 70));
            AnchorBottom(UIFactory.TextButton(_canvas.transform, "活动（敬请期待）", 40, new Vector2(400, 100), new Vector2(310, 70), () =>
            {
                ShowToast("活动系统将在后续版本开放");
            }).transform, new Vector2(310, 70));
        }

        /// <summary>把 UI 元素重新锚定到屏幕底部（y 为相对底边的位置）。</summary>
        private static void AnchorBottom(Transform t, Vector2 pos)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
        }

        private void OnPlayClicked()
        {
            int next = Mathf.Min(SaveSystem.MaxUnlockedLevel, LevelDatabase.Count);
            if (next < 1) next = 1;
            GameFlow.Instance.StartLevel(next);
        }

        private void BuildLevelGrid()
        {
            foreach (Transform child in _gridRoot) Destroy(child.gameObject);
            int unlocked = SaveSystem.MaxUnlockedLevel;
            int cols = 3;
            float cellW = 260, cellH = 170;   // 适配面板内容区（缎带下方至底边约 670px）
            int total = LevelDatabase.Count;
            int rows = Mathf.CeilToInt(total / (float)cols); // 网格纵向居中
            for (int i = 1; i <= total; i++)
            {
                int row = (i - 1) / cols;
                int col = (i - 1) % cols;
                float x = -cellW * cols * 0.5f + cellW * (col + 0.5f);
                float y = (rows - 1) * cellH * 0.5f - row * cellH;
                var node = UIFactory.Anchored(_gridRoot, "Node_" + i, new Vector2(0.5f, 0.5f), new Vector2(cellW - 20, cellH - 10), new Vector2(x, y));

                // 节点类型：完成/当前/锁定/困难（困难关用美术自带的困难节点图，无需文字标签）
                Sprite sprite;
                bool isLocked = i > unlocked;
                bool isCurrent = i == unlocked;
                var def = LevelDatabase.Get(i);
                bool isHard = def != null && def.difficulty == "HARD";
                if (isLocked) sprite = SpriteLib.LevelNode(2);
                else if (isCurrent) sprite = SpriteLib.LevelNode(1);
                else if (isHard) sprite = SpriteLib.LevelNode(3);
                else sprite = SpriteLib.LevelNode(0);

                var btn = UIFactory.IconButton(node, sprite, new Vector2(150, 150), new Vector2(0, -4), null);
                int levelId = i;
                btn.onClick.AddListener(() =>
                {
                    SoundManager.Instance.Play("click");
                    GameFlow.Instance.StartLevel(levelId);
                });
                btn.interactable = !isLocked;

                // 节点数字：当前关节点中心是白色星星 → 用深色数字；其余用白色+深描边
                if (isCurrent)
                {
                    UIFactory.Text(node, "Num", i.ToString(), 48, new Color(0.35f, 0.22f, 0.5f), new Vector2(100, 70), new Vector2(0, -4));
                }
                else
                {
                    var num = UIFactory.Text(node, "Num", i.ToString(), 48, isLocked ? new Color(1f, 1f, 1f, 0.75f) : Color.white, new Vector2(100, 70), new Vector2(0, -4));
                    var ol = num.gameObject.AddComponent<UnityEngine.UI.Outline>();
                    ol.effectColor = new Color(0.15f, 0.1f, 0.25f, 0.9f);
                    ol.effectDistance = new Vector2(1.8f, -1.8f);
                }
                // 锁定关：数字下方、锁图标内标注状态
                if (isLocked)
                    UIFactory.Text(node, "LockHint", "未解锁", 22, new Color(1f, 1f, 1f, 0.62f), new Vector2(120, 28), new Vector2(0, -54));
            }
        }

        private void RefreshTopBar()
        {
            if (_lifeText == null) return;
            int lives = SaveSystem.Lives;
            if (SaveSystem.UnlimitedLife)
            {
                _lifeText.text = "∞";
                _lifeRegenText.text = SaveSystem.UnlimitedRemainingText();
            }
            else
            {
                _lifeText.text = lives + "/" + GameConfig.MaxLives;
                _lifeRegenText.text = lives >= GameConfig.MaxLives ? "" : SaveSystem.LifeRegenText();
            }
            _goldText.text = SaveSystem.Gold.ToString();
            _starText.text = SaveSystem.Stars.ToString();
        }

        // ---------------- 弹窗 ----------------

        private void ShowSettings()
        {
            var modal = OpenModal("SettingsPanel");
            UIFactory.Text(modal, "Title", "设置", 72, Color.white, new Vector2(700, 100), new Vector2(0, 440));
            UIFactory.TextButton(modal, SaveSystem.SoundOn ? "音效：开" : "音效：关", 52, new Vector2(560, 120), new Vector2(0, 160), () =>
            {
                SaveSystem.SoundOn = !SaveSystem.SoundOn;
                CloseModal();
                ShowSettings();
            });
            UIFactory.TextButton(modal, "测试：+5000 金币", 46, new Vector2(560, 110), new Vector2(0, 20), () =>
            {
                SaveSystem.AddGold(5000);
                CloseModal();
            });
            UIFactory.TextButton(modal, "测试：无限生命 2 小时", 46, new Vector2(560, 110), new Vector2(0, -100), () =>
            {
                SaveSystem.GrantUnlimitedLifeMinutes(120);
                CloseModal();
            });
            UIFactory.TextButton(modal, "关闭", 52, new Vector2(560, 120), new Vector2(0, -240), CloseModal);
        }

        private void ShowShop()
        {
            var modal = OpenModal("ShopPanel");
            UIFactory.Text(modal, "Title", "商城（建设中）", 68, Color.white, new Vector2(800, 100), new Vector2(0, 440));
            UIFactory.Text(modal, "Info",
                "正式版将提供：\n· 新手礼包 / 金币档位（首购双倍）\n· 月卡与通行证\n· 所有付费操作二次确认",
                44, new Color(1f, 1f, 1f, 0.9f), new Vector2(760, 320), new Vector2(0, 80));
            UIFactory.TextButton(modal, "测试：+1000 金币", 48, new Vector2(560, 110), new Vector2(0, -160), () =>
            {
                SaveSystem.AddGold(1000);
                CloseModal();
            });
            UIFactory.TextButton(modal, "关闭", 52, new Vector2(560, 120), new Vector2(0, -310), CloseModal);
        }

        private void ShowToast(string msg)
        {
            EffectsRunner.Instance.FloatText(_canvas.transform, msg, new Vector2(0, -900), Color.white, 44);
        }

        private GameObject OpenModal(string name)
        {
            CloseModal();
            var dim = UIFactory.Panel(_canvas.transform, name + "_Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.55f));
            dim.GetComponent<Image>().raycastTarget = true;
            var modal = UIFactory.MakeImage(dim.transform, name, SpriteLib.PanelBase(), new Vector2(900, 1150), Vector2.zero, true, false);
            modal.type = Image.Type.Simple;
            modal.raycastTarget = true;
            StartCoroutine(EffectsRunner.Instance.PopScale(modal.rectTransform));
            _modal = dim.gameObject;
            return modal.gameObject;
        }

        private void CloseModal()
        {
            if (_modal != null) Destroy(_modal);
            _modal = null;
        }
    }
}
