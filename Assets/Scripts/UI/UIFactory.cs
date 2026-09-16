using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StarManor
{
    /// <summary>UGUI 程序化构建工具（动态加载系统中文字体，避免 TMP 依赖）。</summary>
    public static class UIFactory
    {
        private static Font _font;

        public static Font GetFont()
        {
            if (_font == null)
            {
                string[] candidates = { "Microsoft YaHei", "微软雅黑", "SimHei", "Arial" };
                _font = UnityEngine.Font.CreateDynamicFontFromOSFont(candidates, 40);
            }
            return _font;
        }

        public static Canvas CreateCanvas(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameConfig.DesignWidth, GameConfig.DesignHeight);
            scaler.matchWidthOrHeight = 1f; // 以高度为基准适配竖屏
            if (parent != null) go.transform.SetParent(parent, false);
            return canvas;
        }

        public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = go.GetComponent<Image>();
            img.color = color;
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
            img.raycastTarget = false;
            return rt;
        }

        public static RectTransform Anchored(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        public static UnityEngine.UI.Image MakeImage(Transform parent, string name, Sprite sprite, Vector2 size, Vector2 pos, bool raycast = false, bool preserveAspect = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.raycastTarget = raycast;
            img.preserveAspect = preserveAspect;
            return img;
        }

        /// <summary>背景图等比 Cover 适配：铺满画布并裁掉超出部分（背景素材为 1536x1024 横版）。
        /// 注意：CanvasScaler 为 match-height（matchWidthOrHeight=1），实际画布高恒为 DesignHeight，宽随屏幕比例变化。</summary>
        public static void FitCover(RectTransform rt, Vector2 texSize)
        {
            float canvasH = GameConfig.DesignHeight;
            float canvasW = canvasH * (float)Screen.width / Mathf.Max(1, Screen.height);
            float s = Mathf.Max(canvasW / texSize.x, canvasH / texSize.y);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(texSize.x * s, texSize.y * s);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        public static UnityEngine.UI.Image MakeImage(GameObject parent, string name, Sprite sprite, Vector2 size, Vector2 pos, bool raycast = false)
        {
            return MakeImage(parent.transform, name, sprite, size, pos, raycast);
        }

        public static Text Text(Transform parent, string name, string content, int size, Color color,
            Vector2 sizeDelta, Vector2 pos, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = pos;
            var t = go.GetComponent<Text>();
            t.font = GetFont();
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Text Text(GameObject parent, string name, string content, int size, Color color,
            Vector2 sizeDelta, Vector2 pos, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            return Text(parent.transform, name, content, size, color, sizeDelta, pos, anchor);
        }

        public static Button TextButton(Transform parent, string label, int fontSize, Vector2 size, Vector2 pos,
            UnityAction onClick, Sprite bg = null, Color textColor = default(Color))
        {
            var rt = Anchored(parent, "Btn_" + label, new Vector2(0.5f, 0.5f), size, pos);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = bg != null ? bg : SpriteLib.BtnNormal();
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var st = btn.spriteState;
            st.pressedSprite = SpriteLib.BtnPressed();
            st.disabledSprite = SpriteLib.BtnDisabled();
            btn.spriteState = st;
            btn.onClick.AddListener(() =>
            {
                SoundManager.Instance.Play("click");
                if (onClick != null) onClick();
            });
            var col = textColor == default(Color) ? new Color(0.29f, 0.25f, 0.39f) : textColor;
            var t = Text(rt, "Label", label, fontSize, col, size, Vector2.zero);
            // 拉伸锚点铺满按钮（默认中心锚点下 offsetMin/Max 会得到负尺寸矩形，文字不可见）；
            // 底边距大于顶边距做光学补偿：雅黑行盒基线偏高，几何居中时字看起来偏下
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(24, 18);
            t.rectTransform.offsetMax = new Vector2(-24, -4);
            t.alignment = TextAnchor.MiddleCenter;
            // 浅色描边保证在任意按钮底图上的可读性
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.9f);
            outline.effectDistance = new Vector2(1.6f, -1.6f);
            return btn;
        }

        public static Button TextButton(GameObject parent, string label, int fontSize, Vector2 size, Vector2 pos,
            UnityAction onClick, Sprite bg = null, Color textColor = default(Color))
        {
            return TextButton(parent.transform, label, fontSize, size, pos, onClick, bg, textColor);
        }

        public static Button IconButton(Transform parent, Sprite icon, Vector2 size, Vector2 pos, UnityAction onClick)
        {
            var rt = Anchored(parent, "IconBtn", new Vector2(0.5f, 0.5f), size, pos);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                SoundManager.Instance.Play("click");
                if (onClick != null) onClick();
            });
            return btn;
        }

        /// <summary>简单纵向进度条（空槽 + 填充）。</summary>
        public static Image ProgressBar(Transform parent, string name, Vector2 size, Vector2 pos, out Image fill)
        {
            var root = MakeImage(parent, name, SpriteLib.BarEmpty(), size, pos);
            root.type = Image.Type.Sliced;
            fill = MakeImage(root.transform, "Fill", SpriteLib.BarFillGold(), size - new Vector2(56, 60), Vector2.zero);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            return root;
        }
    }
}
