using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloseSettingPanel : MonoBehaviour
{
    public GameObject settingsPanel;
    public void ClosePanel()
        {
        if(settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }
}
