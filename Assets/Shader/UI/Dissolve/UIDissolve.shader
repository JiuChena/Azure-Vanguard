// ============================================================================
// UI 噪声溶解效果（UGUI，纯 Alpha 溶解）
// 用法：创建材质（Shader: AzureVanguard/UI/DissolveNoise）挂到 Image 上，
//       运行时通过 material.SetFloat("_DissolveAmount", 0~1) 驱动溶解进度。
// 行为约定：
//   - 以 Image 组件的 Sprite（[PerRendererData] _MainTex）为主，Tint 仅做乘色；
//   - 溶解度为 0 时完整显示 UI 图片；
//   - 溶解只作用于 alpha（噪声低于阈值的像素变全透明），不混合、不修改 RGB；
//   - 溶解度为 1 时整图完全消失，无残留。
// 噪声图：灰度噪声图（Wrap Mode 建议 Repeat），红通道作为溶解密度。
// 兼容性：支持 Mask / RectMask2D（模板测试与 _ClipRect 裁剪）、UI 顶点色、
//         Sprite 打包图集（CanUseSpriteAtlas）；URP 下按 SRPDefaultUnlit 渲染。
// ============================================================================

Shader "AzureVanguard/UI/DissolveNoise"
{
    Properties
    {
        // PerRendererData：纹理由 Image/SpriteRenderer 提供，不显示在材质面板
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        // 溶解参数
        [NoScaleOffset] _NoiseTex ("Noise Texture (灰度噪声)", 2D) = "white" {}
        _NoiseScale ("Noise Scale (噪声密度)", Range(0.1, 20)) = 3
        _DissolveAmount ("Dissolve Amount (溶解进度 0-1)", Range(0, 1)) = 0

        // UGUI 遮罩支持（与 UI/Default 一致）
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        // 模板状态：交由 UGUI Mask 组件改写，实现遮挡裁剪
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            // 由 UGUI 按遮罩类型自动开启：RectMask2D/Mask 裁剪、Alpha 裁剪
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                // 世界坐标，供 _ClipRect 矩形裁剪使用
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            // UGUI 全局：图集边缘扩张色与矩形裁剪框，由 CanvasRenderer 注入
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            sampler2D _NoiseTex;
            float _NoiseScale;
            float _DissolveAmount;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 采样噪声红通道作为溶解密度场
                float noise = tex2D(_NoiseTex, IN.texcoord * _NoiseScale).r;

                // 溶解遮罩仅作用于 alpha：噪声值低于进度阈值的像素全透明。
                // 两端精确处理：进度 0 完整显示（含噪声为 0 的像素），
                // 进度 1 完全消失（噪声图纯白像素恰为 1，必须显式判死）。
                float amount = saturate(_DissolveAmount);
                float visible = amount <= 0.0 ? 1.0 : (amount >= 1.0 ? 0.0 : step(amount, noise));

                // 基础 UI 颜色：纹理 + 全局扩张色 × 顶点色 × Tint（RGB 不参与溶解混合）
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                color.a *= visible;

                #ifdef UNITY_UI_CLIP_RECT
                // RectMask2D 矩形裁剪
                float mask = UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                color.a *= mask;
                #endif

                // Blend One OneMinusSrcAlpha 要求预乘 alpha；alpha 为 0 时该像素无输出
                color.rgb *= color.a;

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
