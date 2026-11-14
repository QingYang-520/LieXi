using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 关卡存档（轻量版）：只记录"上次玩到哪个场景"，不记录关卡内的任何进度。
/// - 每次进入非主界面的场景时刷新存档（主界面不会覆盖存档）
/// - 退出/切后台时兜底保存
/// - 主界面点"开始游戏"时读取：有存档续玩存档的关卡，没存档进按钮指定的默认关卡（序章）
/// 通过 [RuntimeInitializeOnLoadMethod] 自动启动，无需挂到任何物体上。
/// </summary>
public class SceneSaveLoad : MonoBehaviour
{
    private const string Key = "LastScene";
    private const string MenuScene = "MainMenu";
    private static bool booted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (booted)
            return;
        booted = true;

        var go = new GameObject("SceneSaveLoad");
        DontDestroyOnLoad(go);
        go.AddComponent<SceneSaveLoad>();
    }

    // 供"开始游戏"等按钮调用：有存档续玩存档的关卡，否则进 defaultScene
    public static void LoadSavedOrDefault(string defaultScene)
    {
        string saved = PlayerPrefs.GetString(Key, "");
        if (!string.IsNullOrEmpty(saved) && saved != MenuScene && SceneIndexByName(saved) >= 0)
            SceneManager.LoadScene(saved);
        else if (!string.IsNullOrEmpty(defaultScene))
            SceneManager.LoadScene(defaultScene);
    }

    private static void SaveCurrent(string sceneName)
    {
        // 主界面不覆盖存档：保证"开始游戏"总能续到上次玩的关卡
        if (string.IsNullOrEmpty(sceneName) || sceneName == MenuScene)
            return;
        PlayerPrefs.SetString(Key, sceneName);
        PlayerPrefs.Save();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SaveCurrent(scene.name);
    }

    private void OnApplicationQuit()
    {
        SaveCurrent(SceneManager.GetActiveScene().name);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveCurrent(SceneManager.GetActiveScene().name);
    }

    // 在 Build Settings 的场景列表里按名字查找构建索引；找不到返回 -1
    private static int SceneIndexByName(string name)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string n = Path.GetFileNameWithoutExtension(path);
            if (n == name)
                return i;
        }
        return -1;
    }
}
