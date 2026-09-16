using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace StarManor
{
    /// <summary>轻量特效运行器：爆发粒子帧、飘字、全屏闪光、矩形补间。</summary>
    public class EffectsRunner : MonoBehaviour
    {
        private static EffectsRunner _instance;

        public static EffectsRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("EffectsRunner");
                    _instance = go.AddComponent<EffectsRunner>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>消除爆发：缩放放大 + 淡出后销毁。</summary>
        public void Burst(Transform parent, Vector2 pos, Sprite sprite, float scale = 1f)
        {
            StartCoroutine(BurstRoutine(parent, pos, sprite, scale));
        }

        /// <summary>延迟爆发的消除特效（用于大面积消除的波浪式展开）。</summary>
        public void BurstDelayed(Transform parent, Vector2 pos, Sprite sprite, float scale, float delay)
        {
            StartCoroutine(BurstDelayedRoutine(parent, pos, sprite, scale, delay));
        }

        private IEnumerator BurstDelayedRoutine(Transform parent, Vector2 pos, Sprite sprite, float scale, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return BurstRoutine(parent, pos, sprite, scale);
        }

        private IEnumerator BurstRoutine(Transform parent, Vector2 pos, Sprite sprite, float scale)
        {
            if (sprite == null) yield break;
            var img = UIFactory.MakeImage(parent, "fx_burst", sprite, new Vector2(130, 130) * scale, pos);
            img.color = new Color(1f, 1f, 1f, 0.95f);
            float t = 0f;
            float dur = 0.55f; // 放慢：让玩家看清爆开过程
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                img.rectTransform.localScale = Vector3.one * (0.6f + 0.9f * k);
                img.color = new Color(1f, 1f, 1f, 0.95f * (1f - k * k)); // 后段加速淡出，前段停留更久
                yield return null;
            }
            Object.Destroy(img.gameObject);
        }

        /// <summary>飘字提示。</summary>
        public void FloatText(Transform parent, string text, Vector2 pos, Color color, int fontSize = 56)
        {
            StartCoroutine(FloatTextRoutine(parent, text, pos, color, fontSize));
        }

        private IEnumerator FloatTextRoutine(Transform parent, string text, Vector2 pos, Color color, int fontSize)
        {
            var t = UIFactory.Text(parent, "float_text", text, fontSize, color, new Vector2(600, 90), pos);
            var rt = t.rectTransform;
            float dur = 1.4f; // 放慢：组合技名称/激活提示停留更久
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                rt.anchoredPosition = pos + Vector2.up * (elapsed * 55f);
                float fade = elapsed / dur;
                t.color = new Color(color.r, color.g, color.b, fade < 0.7f ? 1f : 1f - (fade - 0.7f) / 0.3f);
                yield return null;
            }
            Object.Destroy(t.gameObject);
        }

        /// <summary>全屏闪光（组合技演出）。</summary>
        public void Flash(Canvas canvas, Color color, float dur = 0.45f)
        {
            StartCoroutine(FlashRoutine(canvas, color, dur));
        }

        private IEnumerator FlashRoutine(Canvas canvas, Color color, float dur)
        {
            var rt = UIFactory.Panel(canvas.transform, "fx_flash",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color);
            var img = rt.GetComponent<Image>();
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                img.color = new Color(color.r, color.g, color.b, color.a * (1f - t / dur));
                yield return null;
            }
            Object.Destroy(rt.gameObject);
        }

        /// <summary>彩虹波扩散。</summary>
        public void RainbowWave(Transform parent, Vector2 pos)
        {
            StartCoroutine(WaveRoutine(parent, pos));
        }

        private IEnumerator WaveRoutine(Transform parent, Vector2 pos)
        {
            var img = UIFactory.MakeImage(parent, "fx_wave", SpriteLib.FxRainbowWave(), new Vector2(200, 200), pos);
            float t = 0f;
            float dur = 0.8f; // 放慢：彩虹波扩散清晰可见
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                img.rectTransform.localScale = Vector3.one * (0.3f + 4.5f * k);
                img.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }
            Object.Destroy(img.gameObject);
        }

        /// <summary> RectTransform 位置补间。</summary>
        public IEnumerator MoveRect(RectTransform rt, Vector2 from, Vector2 to, float dur)
        {
            float t = 0f;
            if (dur <= 0f) { rt.anchoredPosition = to; yield break; }
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                rt.anchoredPosition = Vector2.Lerp(from, to, k);
                yield return null;
            }
            rt.anchoredPosition = to;
        }

        /// <summary>缩放弹出动画。</summary>
        public IEnumerator PopScale(RectTransform rt, float from = 0.2f, float dur = 0.25f)
        {
            float t = 0f;
            rt.localScale = Vector3.one * from;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float overshoot = 1.1f * Mathf.Sin(k * Mathf.PI);
                rt.localScale = Vector3.one * Mathf.Lerp(from, 1f, k) + Vector3.one * overshoot * 0.1f;
                yield return null;
            }
            rt.localScale = Vector3.one;
        }
    }
}
