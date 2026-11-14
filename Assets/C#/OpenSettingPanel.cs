using UnityEngine;

public class OpenSettingPanel : MonoBehaviour
{
    public GameObject settingPanel;

    public void OpenPanel()
    {
        if (settingPanel != null)
            settingPanel.SetActive(true);
    }

    public void ClosePanel()
    {
        if (settingPanel != null)
            settingPanel.SetActive(false);
    }

    // 所有平台：按 ESC 开/关设置面板（安卓上 ESC 就是返回键）
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingPanel == null)
                return;
            if (settingPanel.activeSelf)
                ClosePanel();
            else
                OpenPanel();
        }
    }
}
