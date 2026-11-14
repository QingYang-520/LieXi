using UnityEngine;

/// <summary>
/// 控制移动端 UI（虚拟按键）的显示：
/// - 电脑端打包后隐藏
/// - 手机/平板等触屏设备显示
/// - 设备外接了鼠标时也隐藏（键鼠党用键鼠操作，拔掉鼠标虚拟键自动回来）
/// - Unity 编辑器里默认保持显示，方便调整按键布局；取消勾选 showInEditor 可预览隐藏状态
/// 本组件挂在 UI 根节点上，根节点保持激活以便持续检测鼠标；
/// 实际被隐藏/显示的是根节点的直接子物体（DPad、BtnAttack 等）。
/// </summary>
[DisallowMultipleComponent]
public class MobileOnlyUI : MonoBehaviour
{
    [Tooltip("勾选：Unity 编辑器里保持显示（方便预览调整）；取消勾选：编辑器里也隐藏")]
    [SerializeField] private bool showInEditor = true;

    private bool shown = true;

    private void Awake()
    {
        ApplyState(true);
    }

    private void Update()
    {
        ApplyState(false);
    }

    private void ApplyState(bool force)
    {
        bool shouldShow = Application.isMobilePlatform && !Input.mousePresent;
        if (Application.isEditor)
            shouldShow = showInEditor;

        if (!force && shouldShow == shown)
            return;
        shown = shouldShow;

        foreach (Transform child in transform)
            child.gameObject.SetActive(shouldShow);
    }
}
