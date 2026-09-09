using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// CharacterEyeMouth 卡通眼嘴着色器的分组材质面板编辑器（结构参考 GeneralToonyShadeEditor）。
/// </summary>
public class CharacterEyeMouthEditor : ShaderGUI
{
    #region Utilities

    // BoxScope 样式：所有分组的外框背景。
    static GUIStyle boxScopeStyle;
    public static GUIStyle BoxScopeStyle
    {
        get
        {
            if (boxScopeStyle == null)
            {
                boxScopeStyle = new GUIStyle(EditorStyles.helpBox);
                var p = boxScopeStyle.padding;
                p.right += 6;
                p.top += 1;
                p.left += 3;
            }
            return boxScopeStyle;
        }
    }

    // ToonLabel 样式：分组标题的粗体大标签。
    static GUIStyle toonLabelStyle;
    public static GUIStyle ToonLabelStyle
    {
        get
        {
            if (toonLabelStyle == null)
            {
                toonLabelStyle = new GUIStyle(EditorStyles.whiteLargeLabel);
                toonLabelStyle.fontStyle = FontStyle.Bold;
            }
            return toonLabelStyle;
        }
    }
    #endregion

    #region MaterialProperties

    //贴图区
    MaterialProperty eyeTexture = null;
    MaterialProperty mouthTexture = null;
    MaterialProperty eyeMouthMask = null;
    MaterialProperty mouthSize = null;
    MaterialProperty eyeBrightness = null;
    MaterialProperty mouthIndex = null;
    MaterialProperty cutoff = null;

    //嘴巴漫反射区
    MaterialProperty mouthDiffuseSteps = null;
    MaterialProperty mouthDiffuseSmooth = null;
    MaterialProperty mouthDiffuseWrap = null;
    MaterialProperty mouthHColor = null;
    MaterialProperty mouthShadowColor = null;
    MaterialProperty mouthMainLightDiffuseScale = null;
    MaterialProperty mouthIndirectLightScale = null;

    //眼睛受光区
    MaterialProperty eyeLightScale = null;
    MaterialProperty eyeLightMax = null;

    //视差区
    MaterialProperty parallaxCenter = null;
    MaterialProperty parallaxScale = null;
    MaterialProperty parallaxMaskEdge = null;
    MaterialProperty parallaxMaskEdgeOffset = null;
    MaterialProperty parallaxEllipse = null;

    //调试区
    MaterialProperty debugEyeLight = null;
    MaterialProperty debugParallax = null;

    //模板测试区
    MaterialProperty stencilMode = null;
    MaterialProperty stencilRef = null;
    MaterialProperty stencilCompare = null;
    MaterialProperty stencilForwardComp = null;
    MaterialProperty stencilForwardOp = null;

    //半透明区
    MaterialProperty blendSrc = null;
    MaterialProperty blendDst = null;
    MaterialProperty transparency = null;

    #endregion

    #region EditorVariables

    //材质编辑器与当前材质实例，供分组绘制使用。
    MaterialEditor m_MaterialEditor;

    #endregion

    /// <summary>
    /// 按名称绑定 CharacterEyeMouth.shader 的全部材质属性。
    /// </summary>
    /// <param name="props">材质面板传入的属性数组。</param>
    public void FindProperties(MaterialProperty[] props)
    {
        eyeTexture = FindProperty("_EyeTexture", props);
        mouthTexture = FindProperty("_MouthTexture", props);
        eyeMouthMask = FindProperty("_EyeMouthMask", props);
        mouthSize = FindProperty("_MouthSize", props);
        eyeBrightness = FindProperty("_EyeBrightness", props);
        mouthIndex = FindProperty("_MouthIndex", props);
        cutoff = FindProperty("_Cutoff", props);

        mouthDiffuseSteps = FindProperty("_DiffuseSteps", props);
        mouthDiffuseSmooth = FindProperty("_DiffuseSmooth", props);
        mouthDiffuseWrap = FindProperty("_DiffuseWrap", props);
        mouthHColor = FindProperty("_HColor", props);
        mouthShadowColor = FindProperty("_ShadowColor", props);
        mouthMainLightDiffuseScale = FindProperty("_MainLightDiffuseScale", props);
        mouthIndirectLightScale = FindProperty("_IndirectlightScale", props);

        eyeLightScale = FindProperty("_EyeLightScale", props);
        eyeLightMax = FindProperty("_EyeLightMax", props);

        parallaxCenter = FindProperty("_ParallaxCenter", props);
        parallaxScale = FindProperty("_ParallaxScale", props);
        parallaxMaskEdge = FindProperty("_ParallaxMaskEdge", props);
        parallaxMaskEdgeOffset = FindProperty("_ParallaxMaskEdgeOffset", props);
        parallaxEllipse = FindProperty("_ParallaxEllipse", props);

        debugEyeLight = FindProperty("_DebugEyeLight", props);
        debugParallax = FindProperty("_DebugParallax", props);

        stencilMode = FindProperty("_StencilMode", props);
        stencilRef = FindProperty("_StencilRef", props);
        stencilCompare = FindProperty("_StencilCompare", props);
        stencilForwardComp = FindProperty("_StencilForwardComp", props);
        stencilForwardOp = FindProperty("_StencilForwardOp", props);

        blendSrc = FindProperty("_BlendSrc", props);
        blendDst = FindProperty("_BlendDst", props);
        transparency = FindProperty("_Transparency", props);
    }

    /// <summary>
    /// 材质面板主入口，按分组绘制全部属性。
    /// </summary>
    /// <param name="materialEditor">材质编辑器实例。</param>
    /// <param name="props">材质面板传入的属性数组。</param>
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
    {
        FindProperties(props);
        m_MaterialEditor = materialEditor;

        SyncStencilMode();
        ShaderPropertiesGUI();
    }

    /// <summary>
    /// 按模板测试模式同步前向 pass 的模板行为：写入=Always+Replace（标记），读取=比较+Keep（门控），关闭=Always+Keep（中立）。
    /// </summary>
    private void SyncStencilMode()
    {
        float mode = stencilMode.floatValue;
        float targetComp = 8f; // Always
        float targetOp = 0f;   // Keep
        if (mode >= 2f) // Read：比较通过才渲染
        {
            targetComp = stencilCompare.floatValue;
        }
        else if (mode >= 1f) // Write：写标记
        {
            targetOp = 2f;   // Replace
        }
        if (!Mathf.Approximately(stencilForwardComp.floatValue, targetComp))
            stencilForwardComp.floatValue = targetComp;
        if (!Mathf.Approximately(stencilForwardOp.floatValue, targetOp))
            stencilForwardOp.floatValue = targetOp;
    }

    /// <summary>
    /// 按顺序绘制各分组：贴图、嘴巴漫反射、眼睛受光、视差、半透明、模板测试、调试、高级设置。
    /// </summary>
    private void ShaderPropertiesGUI()
    {
        MainEditor();
        MouthDiffuseEditor();
        EyeLightEditor();
        ParallaxEditor();
        TransparencyEditor();
        StencilEditor();
        DebugEditor();
        AdvancedEditor();
    }

    #region HelperFunctions

    /// <summary>
    /// 绘制一个带标题的外框分组，内部依次绘制给定属性。
    /// </summary>
    /// <param name="header">分组标题。</param>
    /// <param name="props">分组内要绘制的属性列表。</param>
    private void DrawBoxSpace(string header, List<MaterialProperty> props)
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        GUILayout.Label(header, ToonLabelStyle);

        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        foreach (var prop in props)
        {
            DrawProperty(prop);
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制开关外框分组：标题显示开关，开启时展开参数列表。
    /// </summary>
    /// <param name="header">作为开关的属性。</param>
    /// <param name="props">开启后要绘制的属性列表。</param>
    /// <param name="name">自定义标题；为空时用开关属性展示名。</param>
    private void DrawToggleBoxScope(MaterialProperty header, List<MaterialProperty> props, string name = null)
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        DrawToggleHeader(header, name);

        bool isParamPropEnabled = !Mathf.Approximately(header.floatValue, 0f);
        if (isParamPropEnabled && props.Count > 0)
        {
            EditorGUILayout.BeginVertical(BoxScopeStyle);
            EditorGUILayout.Space(2);

            foreach (var prop in props)
            {
                DrawProperty(prop);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 按属性的展示名绘制单个属性。
    /// </summary>
    /// <param name="prop">要绘制的材质属性。</param>
    private void DrawProperty(MaterialProperty prop)
    {
        m_MaterialEditor.ShaderProperty(prop, prop.displayName);
    }

    /// <summary>
    /// 绘制开关行：左侧粗体标题，右侧开关控件。
    /// </summary>
    /// <param name="prop">开关属性。</param>
    /// <param name="name">自定义标题；为空时用属性展示名。</param>
    private void DrawToggleHeader(MaterialProperty prop, string name = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            name = prop.displayName.Replace("Use", "");
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(name, ToonLabelStyle);
        m_MaterialEditor.ShaderProperty(prop, string.Empty);

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }

    #endregion

    #region EditorFunctions

    /// <summary>
    /// 绘制贴图组：眼睛/嘴部/遮罩贴图与翻转册、亮度、裁剪参数。
    /// </summary>
    private void MainEditor()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        GUILayout.Label("Eye & Mouth", ToonLabelStyle);

        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Eye Texture"), eyeTexture);
        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Mouth Texture"), mouthTexture);
        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Eye Mouth Mask"), eyeMouthMask);
        DrawProperty(mouthSize);
        DrawProperty(mouthIndex);
        DrawProperty(eyeBrightness);
        DrawProperty(cutoff);

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制嘴巴漫反射组：色阶化与明暗参数。
    /// </summary>
    private void MouthDiffuseEditor()
    {
        DrawBoxSpace("Mouth Diffuse",
            new List<MaterialProperty>
            {
                mouthDiffuseSteps, mouthDiffuseSmooth, mouthDiffuseWrap, mouthMainLightDiffuseScale,
                mouthHColor, mouthShadowColor, mouthIndirectLightScale
            });
    }

    /// <summary>
    /// 绘制眼睛受光组：眼睛提亮受光影响的 Scale 与上限。
    /// </summary>
    private void EyeLightEditor()
    {
        DrawBoxSpace("Eye Light",
            new List<MaterialProperty>
            {
                eyeLightScale, eyeLightMax
            });
    }

    /// <summary>
    /// 绘制视差组：视线偏移参数（中心、强度、半径、柔化、椭圆缩放）。
    /// </summary>
    private void ParallaxEditor()
    {
        DrawBoxSpace("Parallax",
            new List<MaterialProperty>
            {
                parallaxCenter, parallaxScale, parallaxMaskEdge, parallaxMaskEdgeOffset, parallaxEllipse
            });
    }

    /// <summary>
    /// 绘制半透明组：混合源、混合目标与最终Alpha（配合 Blend 混合实现头发等半透明）。
    /// </summary>
    private void TransparencyEditor()
    {
        DrawBoxSpace("Transparency",
            new List<MaterialProperty>
            {
                blendSrc, blendDst, transparency
            });
    }

    /// <summary>
    /// 绘制模板测试组：写入/读取模式、模板值与比较方式。
    /// </summary>
    private void StencilEditor()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        GUILayout.Label("Stencil Test", ToonLabelStyle);

        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        // 模式下拉框
        int mode = Mathf.RoundToInt(stencilMode.floatValue);
        var newMode = (StencilTestMode)EditorGUILayout.EnumPopup("模板测试模式", (StencilTestMode)mode);
        stencilMode.floatValue = (float)newMode;

        DrawProperty(stencilRef);
        DrawProperty(stencilCompare);

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 模板测试模式枚举（与 shader 属性值对应：0=关闭 1=写入 2=读取）。
    /// </summary>
    private enum StencilTestMode { Off = 0, Write = 1, Read = 2 }

    /// <summary>
    /// 绘制调试组：眼睛提亮度与视差范围可视化开关。
    /// </summary>
    private void DebugEditor()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        GUILayout.Label("Debug", ToonLabelStyle);

        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        DrawToggleHeader(debugEyeLight, "Eye Light Boost Debug");
        DrawToggleHeader(debugParallax, "Parallax Range Debug");

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制高级设置组：渲染队列、实例化与双面全局光照。
    /// </summary>
    private void AdvancedEditor()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        GUILayout.Label("Advanced", ToonLabelStyle);

        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        m_MaterialEditor.RenderQueueField();
        m_MaterialEditor.EnableInstancingField();
        m_MaterialEditor.DoubleSidedGIField();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    #endregion
}
