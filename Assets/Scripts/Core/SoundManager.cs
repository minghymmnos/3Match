using UnityEngine;

namespace StarManor
{
    /// <summary>程序生成音效（无外部音频资源依赖）：消除/爆炸/按钮/胜负。</summary>
    public class SoundManager : MonoBehaviour
    {
        private static SoundManager _instance;
        public static SoundManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("SoundManager");
                    _instance = go.AddComponent<SoundManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private AudioClip _pop, _boom, _click, _win, _fail, _combo;
        private AudioSource _src;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _src = gameObject.AddComponent<AudioSource>();
            _pop = MakePop();
            _boom = MakeBoom();
            _click = MakeClick();
            _win = MakeArpeggio(true);
            _fail = MakeArpeggio(false);
            _combo = MakeCombo();
        }

        public void Play(string name)
        {
            if (!SaveSystem.SoundOn) return;
            AudioClip clip = null;
            switch (name)
            {
                case "pop": clip = _pop; break;
                case "boom": clip = _boom; break;
                case "click": clip = _click; break;
                case "win": clip = _win; break;
                case "fail": clip = _fail; break;
                case "combo": clip = _combo; break;
            }
            if (clip != null) _src.PlayOneShot(clip);
        }

        private static float[] Samples(int count)
        {
            return new float[count];
        }

        private AudioClip MakePop()
        {
            int sr = 44100;
            int n = (int)(sr * 0.12f);
            var data = Samples(n);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = 1f - (float)i / n;
                float f = 880f - 600f * ((float)i / n);
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.5f;
            }
            return BuildClip("pop", data, sr);
        }

        private AudioClip MakeBoom()
        {
            int sr = 44100;
            int n = (int)(sr * 0.35f);
            var data = Samples(n);
            var rand = new System.Random(7);
            float last = 0f;
            for (int i = 0; i < n; i++)
            {
                float env = Mathf.Pow(1f - (float)i / n, 1.6f);
                float white = (float)(rand.NextDouble() * 2.0 - 1.0);
                last = Mathf.Lerp(last, white, 0.35f);
                float low = Mathf.Sin(2f * Mathf.PI * 70f * ((float)i / sr)) * (1f - (float)i / n);
                data[i] = Mathf.Clamp(last * 0.8f + low * 0.6f, -1f, 1f) * env * 0.6f;
            }
            return BuildClip("boom", data, sr);
        }

        private AudioClip MakeClick()
        {
            int sr = 44100;
            int n = (int)(sr * 0.06f);
            var data = Samples(n);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = 1f - (float)i / n;
                data[i] = Mathf.Sin(2f * Mathf.PI * 1400f * t) * env * 0.35f;
            }
            return BuildClip("click", data, sr);
        }

        private AudioClip MakeCombo()
        {
            int sr = 44100;
            float[] freqs = { 523f, 659f, 784f, 1047f };
            int n = (int)(sr * 0.4f);
            var data = Samples(n);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float env = 1f - (float)i / n;
                int step = Mathf.Min(freqs.Length - 1, (int)(t * 10f));
                data[i] = Mathf.Sin(2f * Mathf.PI * freqs[step] * t) * env * 0.4f;
            }
            return BuildClip("combo", data, sr);
        }

        private AudioClip MakeArpeggio(bool happy)
        {
            int sr = 44100;
            float[] up = { 523f, 659f, 784f, 1047f, 1319f };
            float[] down = { 659f, 587f, 523f, 440f, 349f };
            float[] freqs = happy ? up : down;
            int stepLen = (int)(sr * 0.14f);
            int n = stepLen * freqs.Length;
            var data = Samples(n);
            for (int s = 0; s < freqs.Length; s++)
            {
                for (int i = 0; i < stepLen; i++)
                {
                    int idx = s * stepLen + i;
                    float t = (float)i / sr;
                    float env = 1f - (float)i / stepLen;
                    data[idx] = Mathf.Sin(2f * Mathf.PI * freqs[s] * t) * env * 0.35f;
                }
            }
            return BuildClip(happy ? "win" : "fail", data, sr);
        }

        private static AudioClip BuildClip(string name, float[] data, int sr)
        {
            var clip = AudioClip.Create(name, data.Length, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
