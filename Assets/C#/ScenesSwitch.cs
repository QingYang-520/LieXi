using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ScenesSwitch : MonoBehaviour
{
#if UNITY_EDITOR
    public SceneAsset sceneAsset;
#endif
    [HideInInspector][SerializeField] private string sceneName;

    public void Switch()   // ← 修正拼写
    {
        // 开始游戏：有存档续玩上次的关卡，没存档进本按钮指定的默认关卡（序章）
        SceneSaveLoad.LoadSavedOrDefault(sceneName);
    }

#if UNITY_EDITOR
    private void OnValidate()   // ← 关键：大写 V
    {
        if (sceneAsset != null)
            sceneName = sceneAsset.name;
    }
#endif
}