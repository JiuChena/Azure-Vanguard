using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// CT 卡通着色器的分组材质面板编辑器。
/// </summary>
public class GeneralToonyShadeEditor : ShaderGUI
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

    //主纹理区
    MaterialProperty albedoMap = null;
    MaterialProperty albedoColor = null;
    MaterialProperty maskTex = null;
    MaterialProperty maskTexThreshold = null;
    MaterialProperty normalMap = null;
    MaterialProperty normalMapScale = null;
    MaterialProperty indirectLightScale = null;
    MaterialProperty ambientScale = null;

    //卡通漫反射区
    MaterialProperty diffuseSteps = null;
    MaterialProperty diffuseSmooth = null;
    MaterialProperty mainLightDiffuseScale = null;
    MaterialProperty diffuseWrap = null;
    MaterialProperty highlightColor = null;
    MaterialProperty shadowColor = null;
    MaterialProperty useShadowBaseMix = null;
    MaterialProperty shadowBaseMix = null;

    //附加光漫反射开关及其参数
    MaterialProperty useAdditionalLightsDiffuse = null;
    MaterialProperty additionalLightsScale = null;

    //高光区
    MaterialProperty specularMap = null;
    MaterialProperty useHairDirectionHighlight = null;
    MaterialProperty hairDirectionHighlightLobeOffset = null;
    MaterialProperty hairSpecTopMultiplier = null;
    MaterialProperty hairSpecTopLeveler = null;
    MaterialProperty hairSpecBotArea = null;
    MaterialProperty hairSpecBotMultiplier = null;
    MaterialProperty hairDirectionHighlightIntensity = null;
    MaterialProperty hairDirectionHighlightSoftness = null;
    MaterialProperty hairDirectionHighlightTangentBlend = null;
    MaterialProperty hairDirectionHighlightChannel = null;
    MaterialProperty specularColor = null;
    MaterialProperty specularScale = null;
    MaterialProperty specularSmoothnessChannel = null;
    MaterialProperty specularSize = null;
    MaterialProperty specularPosterizeSteps = null;
    MaterialProperty specularFaloff = null;
    MaterialProperty additionalSpecularFaloff = null;
    MaterialProperty useSpecular = null;
    MaterialProperty useAdditionalLightsSpecular = null;
    MaterialProperty useEnvironmentReflection = null;
    MaterialProperty envReflectionStrength = null;

    //金属/自发光区
    MaterialProperty useMetal = null;
    MaterialProperty useEmission = null;
    MaterialProperty emissionColor = null;
    MaterialProperty emissionMap = null;
    MaterialProperty emissionIntensity = null;
    //MaskTex 通道分工区（MX）：B=透光（背光响应），A=亮度遮罩
    MaterialProperty glowTint = null;
    MaterialProperty glowStrength = null;
    MaterialProperty glowTransSharpness = null;
    MaterialProperty brightnessMapStrength = null;

    //对比度区
    MaterialProperty contrast = null;

    //模板测试区
    MaterialProperty stencilMode = null;
    MaterialProperty stencilRef = null;
    MaterialProperty stencilCompare = null;
    MaterialProperty stencilForwardComp = null;
    MaterialProperty stencilForwardOp = null;


    //边缘光区
    MaterialProperty rimColor = null;
    MaterialProperty rimMin = null;
    MaterialProperty rimMax = null;
    MaterialProperty rimFresnelSoftness = null;
    MaterialProperty rimTextureWeight = null;
    MaterialProperty useRimLight = null;

    //描边区
    MaterialProperty useOutline = null;
        MaterialProperty outlineMode = null;
        MaterialProperty outlineColor = null;
        MaterialProperty outlineWidth = null;
        MaterialProperty adaptiveWidth = null;
        MaterialProperty outlineMaxScale = null;
        MaterialProperty outlineScreenWidth = null;
        MaterialProperty outlineZCorrection = null;

    //半透明区
    MaterialProperty blendSrc = null;
    MaterialProperty transparencyMap = null;
    MaterialProperty transparencyChannel = null;
    MaterialProperty blendDst = null;
    MaterialProperty transparency = null;

    #endregion

    #region EditorVariables

    //材质编辑器与当前材质实例，供分组绘制使用。
    MaterialEditor m_MaterialEditor;

    #endregion

    /// <summary>
    /// 按名称绑定 CT.shader 的全部材质属性。
    /// </summary>
    /// <param name="props">材质面板传入的属性数组。</param>
    public void FindProperties(MaterialProperty[] props)
    {
        albedoMap = FindProperty("_Albedo", props);
        albedoColor = FindProperty("_Color", props);
        maskTex = FindProperty("_MaskTex", props);
        maskTexThreshold = FindProperty("_MaskTexThreshold", props);
        normalMap = FindProperty("_NormalMap", props);
        normalMapScale = FindProperty("_NormalMapScale", props);
        indirectLightScale = FindProperty("_IndirectlightScale", props);
        ambientScale = FindProperty("_AmbientScale", props);

        diffuseSteps = FindProperty("_DiffuseSteps", props);
        diffuseSmooth = FindProperty("_DiffuseSmooth", props);
        mainLightDiffuseScale = FindProperty("_MainLightDiffuseScale", props);
        diffuseWrap = FindProperty("_DiffuseWrap", props);
        highlightColor = FindProperty("_HColor", props);
        shadowColor = FindProperty("_ShadowColor", props);
        useShadowBaseMix = FindProperty("_UseShadowBaseMix", props);
        shadowBaseMix = FindProperty("_ShadowBaseMix", props);

        useAdditionalLightsDiffuse = FindProperty("_UseAdditionalLightsDiffuse", props);
        additionalLightsScale = FindProperty("_AdditionalLightsScale", props);

        specularMap = FindProperty("_SpecularMap", props);
        useHairDirectionHighlight = FindProperty("_UseHairDirectionHighlight", props);
        hairDirectionHighlightLobeOffset = FindProperty("_HairDirectionHighlightLobeOffset", props);
        hairSpecTopMultiplier = FindProperty("_HairSpecTopMultiplier", props);
        hairSpecTopLeveler = FindProperty("_HairSpecTopLeveler", props);
        hairSpecBotArea = FindProperty("_HairSpecBotArea", props);
        hairSpecBotMultiplier = FindProperty("_HairSpecBotMultiplier", props);
        hairDirectionHighlightIntensity = FindProperty("_HairDirectionHighlightIntensity", props);
        hairDirectionHighlightSoftness = FindProperty("_HairDirectionHighlightSoftness", props);
        hairDirectionHighlightTangentBlend = FindProperty("_HairDirectionHighlightTangentBlend", props);
        hairDirectionHighlightChannel = FindProperty("_HairDirectionHighlightChannel", props);
        specularColor = FindProperty("_SpecularColor", props);
        specularScale = FindProperty("_SpecularScale", props);
        specularSmoothnessChannel = FindProperty("_SpecularSmoothnessChannel", props);
        specularSize = FindProperty("_SpecularSize", props);
        specularPosterizeSteps = FindProperty("_SpecularPosterizeSteps", props);
        specularFaloff = FindProperty("_SpecularFaloff", props);
        additionalSpecularFaloff = FindProperty("_AdditionalSpecularFaloff", props);
        useSpecular = FindProperty("_UseSpecular", props);
        useAdditionalLightsSpecular = FindProperty("_UseAdditionalLightsSpecular", props);
        useEnvironmentReflection = FindProperty("_UseEnvironmentReflection", props);
        envReflectionStrength = FindProperty("_EnvReflectionStrength", props);

        useMetal = FindProperty("_UseMetal", props);
        useEmission = FindProperty("_UseEmission", props);
        emissionColor = FindProperty("_EmissionColor", props);
        emissionMap = FindProperty("_EmissionMap", props);
        emissionIntensity = FindProperty("_EmissionIntensity", props);
        glowTint = FindProperty("_GlowTint", props);
        glowStrength = FindProperty("_GlowStrength", props);
        glowTransSharpness = FindProperty("_GlowTransSharpness", props);
        brightnessMapStrength = FindProperty("_BrightnessMapStrength", props);

        contrast = FindProperty("_Contrast", props);

        stencilMode = FindProperty("_StencilMode", props);
        stencilRef = FindProperty("_StencilRef", props);
        stencilCompare = FindProperty("_StencilCompare", props);
        stencilForwardComp = FindProperty("_StencilForwardComp", props);
        stencilForwardOp = FindProperty("_StencilForwardOp", props);


        rimColor = FindProperty("_RimColor", props);
        rimMin = FindProperty("_RimMin", props);
        rimMax = FindProperty("_RimMax", props);
        rimFresnelSoftness = FindProperty("_RimFresnelSoftness", props);
        rimTextureWeight = FindProperty("_RimTextureWeight", props);
        useRimLight = FindProperty("_UseRimLight", props);

        useOutline = FindProperty("_UseOutline", props);
        outlineMode = FindProperty("_OutlineMode", props);
        outlineColor = FindProperty("_OutlineColor", props);
        outlineWidth = FindProperty("_OutlineWidth", props);
        adaptiveWidth = FindProperty("_AdaptiveWidth", props);
        outlineMaxScale = FindProperty("_OutlineMaxScale", props);
        outlineScreenWidth = FindProperty("_OutlineScreenWidth", props);
        outlineZCorrection = FindProperty("_OutlineZCorrection", props);

        blendSrc = FindProperty("_BlendSrc", props);
        blendDst = FindProperty("_BlendDst", props);
        transparency = FindProperty("_Transparency", props);
        transparencyMap = FindProperty("_TransparencyMap", props);
        transparencyChannel = FindProperty("_TransparencyChannel", props);
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
    /// 按顺序绘制各分组：主纹理、卡通漫反射、高光、边缘光、描边、对比度、模板测试、高级设置。
    /// </summary>
    private void ShaderPropertiesGUI()
    {
        MainEditor();
        DiffuseEditor();
        SpecularEditor();
        RimEditor();
        OutlineEditor();
        TransparencyEditor();
        ContrastEditor();
        StencilEditor();
        Advanced();
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
        EditorGUILayout.Space(7);

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
    /// 绘制贴图通道选择下拉框（R/G/B/A）。
    /// </summary>
    /// <param name="prop">通道属性（0=R 1=G 2=B 3=A）。</param>
    /// <param name="label">显示名。</param>
    private void DrawChannelPopup(MaterialProperty prop, string label)
    {
        int channel = Mathf.RoundToInt(prop.floatValue);
        var newChannel = (TextureChannel)EditorGUILayout.EnumPopup(label, (TextureChannel)channel);
        prop.floatValue = (float)newChannel;
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
    /// 绘制主纹理组：主纹理/主色、法线、遮蔽、间接光。
    /// </summary>
    private void MainEditor()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        GUILayout.Label("Main", ToonLabelStyle);

        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Albedo"), albedoMap, albedoColor);
        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Normal Map"), normalMap, normalMapScale);
        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Mask Tex"), maskTex, maskTexThreshold);

        // MaskTex 通道分工（MX，固定）：G=遮蔽，B=透光（背光响应：光穿透毛发朝相机时增强），A=亮度遮罩，强度 0=关闭
        DrawProperty(glowTint);
        DrawProperty(glowStrength);
        DrawProperty(glowTransSharpness);
        DrawProperty(brightnessMapStrength);

        DrawProperty(indirectLightScale);
        DrawProperty(ambientScale);

        m_MaterialEditor.TextureScaleOffsetProperty(maskTex);

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制卡通漫反射组：色带参数与附加光漫反射开关。
    /// </summary>
    private void DiffuseEditor()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        // Toon Shading 板块：漫反射参数 + 附加光漫反射开关合并展示
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        GUILayout.Label("Toon Shading", ToonLabelStyle);

        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        DrawProperty(diffuseSteps);
        DrawProperty(diffuseSmooth);
        DrawProperty(diffuseWrap);
        DrawProperty(mainLightDiffuseScale);
        DrawProperty(highlightColor);
        DrawProperty(shadowColor);

        // 阴影色混合贴图颜色：开关 + 混合度，并入 Toon Shading 板块
        DrawToggleHeader(useShadowBaseMix, "Shadow Texture Mix");
        if (!Mathf.Approximately(useShadowBaseMix.floatValue, 0f))
        {
            DrawProperty(shadowBaseMix);
        }

        // 附加光漫反射：开关 + 强度，并入 Toon Shading 板块
        DrawToggleHeader(useAdditionalLightsDiffuse, "Additional Lights Diffuse");
        if (!Mathf.Approximately(useAdditionalLightsDiffuse.floatValue, 0f))
        {
            DrawProperty(additionalLightsScale);
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        // 自发光开关
        DrawToggleBoxScope(useEmission,
            new List<MaterialProperty>
            {
                emissionMap, emissionColor, emissionIntensity
            }, "Emission");

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制高光组：高光贴图/颜色，以及高光、附加光高光、环境反射开关。
    /// </summary>
    private void SpecularEditor()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        GUILayout.Label("Specular Shading", ToonLabelStyle);

        // 高光贴图放最前
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Specular Map"), specularMap, specularColor);
        DrawChannelPopup(specularSmoothnessChannel, "光滑度通道");

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        DrawToggleBoxScope(useSpecular,
            new List<MaterialProperty>
            {
                specularScale, specularSize, specularPosterizeSteps, specularFaloff
            }, "Specular Highlights");

        DrawToggleBoxScope(useAdditionalLightsSpecular, new List<MaterialProperty> { additionalSpecularFaloff }, "Additional Lights Specular");

        DrawToggleBoxScope(useEnvironmentReflection,
            new List<MaterialProperty>
            {
                envReflectionStrength
            }, "Environment Reflection");

        // 金属材质开关
        DrawToggleBoxScope(useMetal, new List<MaterialProperty>(), "Metal Material");

        DrawToggleBoxScope(useHairDirectionHighlight,
            new List<MaterialProperty>
            {
                hairDirectionHighlightLobeOffset, hairSpecTopMultiplier,
                hairSpecTopLeveler, hairSpecBotArea, hairSpecBotMultiplier,
                hairDirectionHighlightIntensity, hairDirectionHighlightSoftness,
                hairDirectionHighlightTangentBlend
            }, "Anisotropic Sampling");
        DrawChannelPopup(hairDirectionHighlightChannel, "各向异性响应通道");

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制边缘光组：边缘光开关及其参数。
    /// </summary>
    private void RimEditor()
    {
        DrawToggleBoxScope(useRimLight,
            new List<MaterialProperty>
            {
                rimColor, rimMin, rimMax, rimFresnelSoftness, rimTextureWeight
            }, "Rim Light");
    }

    /// <summary>
    /// 绘制描边组：描边开关、模式选择（下拉框）与各自模式所需的参数。
    /// </summary>
    private void OutlineEditor()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        DrawToggleHeader(useOutline, "Outline");

        if (!Mathf.Approximately(useOutline.floatValue, 0f))
        {
            EditorGUILayout.BeginVertical(BoxScopeStyle);
            EditorGUILayout.Space(2);

            // 模式下拉框
            int mode = Mathf.RoundToInt(outlineMode.floatValue);
            var newMode = (OutlineMode)EditorGUILayout.EnumPopup("描边模式", (OutlineMode)mode);
            outlineMode.floatValue = (float)newMode;

            // 描边颜色（两种模式共用）
            DrawProperty(outlineColor);

            if (newMode == OutlineMode.WorldNormal)
            {
                // 世界空间法线外推：宽度 + 距离自适应
                DrawProperty(outlineWidth);
                DrawProperty(adaptiveWidth);
                DrawProperty(outlineMaxScale);
            }
            else
            {
                // 屏幕空间切线：屏幕像素宽度 + 深度修正
                DrawProperty(outlineScreenWidth);
                DrawProperty(outlineZCorrection);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制半透明组：混合源/目标下拉框 + 透明度贴图/通道 + 最终Alpha（头发半透明）。
    /// </summary>
    private void TransparencyEditor()
    {
        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        GUILayout.Label("Transparency", ToonLabelStyle);

        EditorGUILayout.BeginVertical(BoxScopeStyle);
        EditorGUILayout.Space(2);

        DrawProperty(blendSrc);
        DrawProperty(blendDst);
        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("Transparency Map"), transparencyMap);
        DrawChannelPopup(transparencyChannel, "透明度通道");
        DrawProperty(transparency);

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 绘制对比度组：作用于最终片元输出的总对比度。
    /// </summary>
    private void ContrastEditor()
    {
        DrawBoxSpace("Contrast", new List<MaterialProperty> { contrast });
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
    /// 描边模式枚举（与 shader 属性值对应：0=世界空间法线外推 1=屏幕空间切线膨胀）。
    /// </summary>
    private enum OutlineMode { WorldNormal = 0, ScreenTangent = 1 }

    /// <summary>
    /// 贴图通道枚举（与 shader 属性值对应：0=R 1=G 2=B 3=A）。
    /// </summary>
    private enum TextureChannel { R = 0, G = 1, B = 2, A = 3 }

    /// <summary>
    /// 绘制高级设置组：渲染队列、实例化与双面全局光照。
    /// </summary>
    private void Advanced()
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
