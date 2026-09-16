using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarManor
{
    /// <summary>游戏流程控制：场景切换与当前关卡。</summary>
    public class GameFlow : MonoBehaviour
    {
        private static GameFlow _instance;
        public static GameFlow Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("GameFlow");
                    _instance = go.AddComponent<GameFlow>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        public int CurrentLevelId = 1;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void StartLevel(int levelId)
        {
            CurrentLevelId = levelId;
            Time.timeScale = 1f;
            SceneManager.LoadScene("Gameplay");
        }

        public void BackToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        public static string ActiveSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }
    }
}
