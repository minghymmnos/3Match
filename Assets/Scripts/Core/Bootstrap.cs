using UnityEngine;
using UnityEngine.EventSystems;

namespace StarManor
{
    /// <summary>场景自举：初始化 EventSystem / 全局单例，并按场景挂载对应 UI。</summary>
    public class Bootstrap : MonoBehaviour
    {
        private void Awake()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
            // 全局单例（幂等）
            var flow = GameFlow.Instance;
            var sound = SoundManager.Instance;
            var fx = EffectsRunner.Instance;

            string scene = GameFlow.ActiveSceneName();
            if (scene == "Gameplay")
            {
                if (GetComponent<BoardController>() == null)
                    gameObject.AddComponent<BoardController>();
            }
            else
            {
                if (GetComponent<MainMenuUI>() == null)
                    gameObject.AddComponent<MainMenuUI>();
            }
        }
    }
}
