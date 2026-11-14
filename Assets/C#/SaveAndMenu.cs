using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 保存并退出按钮：设置已实时保存（音量拖动即存），点击返回主界面。
/// 关卡存档保留（SceneSaveLoad 只记场景名且主界面不覆盖存档），
/// 下次在主界面点"开始游戏"会续玩上次的关卡。
/// </summary>
public class SaveAndMenu : MonoBehaviour
{
    public void GoMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
