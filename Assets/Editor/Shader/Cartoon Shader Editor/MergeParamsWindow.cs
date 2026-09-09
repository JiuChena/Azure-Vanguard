using System;
using UnityEditor;
using UnityEngine;

/// <summary>修改合并角度参数的 Modal 弹窗。角度单位度。</summary>
public class MergeParamsWindow : EditorWindow
{
    // 当前编辑的合并角度（度）。
    private float angle = 60f;
    // 确定回调，确定时携带最终角度值。
    private Action<float> onConfirm;

    /// <summary>
    /// 打开合并角度修改弹窗。
    /// </summary>
    /// <param name="currentAngle">当前合并角度（度）。</param>
    /// <param name="confirm">确定回调，参数为（角度°）。</param>
    public static void Show(float currentAngle, Action<float> confirm)
    {
        var win = GetWindow<MergeParamsWindow>(true, "修改合并角度", true);
        win.angle = currentAngle;
        win.onConfirm = confirm;
        win.minSize = win.maxSize = new Vector2(300, 140);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("合并角度", EditorStyles.boldLabel);
        angle = EditorGUILayout.Slider("角度（°）", angle, 0f, 180f);

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox($"当前：{angle:0.#}°。角度越大越易合并大角度转折、描边越平滑；调小则更保守保留硬边。", MessageType.Info);

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("取消"))
        {
            Close();
            return;
        }
        if (GUILayout.Button("确定"))
        {
            onConfirm?.Invoke(angle);
            Close();
        }
        EditorGUILayout.EndHorizontal();
    }
}
