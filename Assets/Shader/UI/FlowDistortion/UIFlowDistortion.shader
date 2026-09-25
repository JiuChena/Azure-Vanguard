// ============================================================================
// UI 流动扰动效果（UGUI Image 专用，URP 兼容）
// 用法：创建材质（Shader: AzureVanguard/UI/FlowDistortion）挂到 Image 上即可，
//       无需任何脚本，噪声流动由 _Time 内置驱动。
// 行为约定：
//   - 以 Image 组件的 Sprite（[PerRendererData] _MainTex）为主，Tint 仅做乘色；
//   - 扰动作用于 UV：解析 simplex fbm 噪声生成随时间流动的偏移场，
//     让图片内容像水面/热气流一样缓慢扭动（背景图"流动"效果）；
//   - 噪声域坐标在顶点阶段计算（uv 缩放 + 时间平移均为线性运算），
//     像素阶段只执行噪声本身，共 4 次 snoise/像素（参考实现为 8 次以上）；
//   - 扰动强度与流动速度均为 0 时输出与原图一致；
//   - Shimmer 提供可选的噪声明暗流动，增强"水面反光"质感，0 时关闭。
// 兼容性：支持 Mask / RectMask2D（模板测试与 _ClipRect 裁剪）、UI 顶点色、
//         Sprite 打包图集（CanUseSpriteAtlas）；URP 下按 SRPDefaultUnlit 渲染。
// 注意：扰动会向图集相邻区域偏移采样，强烈建议 Sprite 关闭 Packable
//       或扰动强度保持在 0.05 以内；全图独立纹理则无此限制。
// ============================================================================

Shader "AzureVanguard/UI/FlowDistortion"
{
    Properties
    {
        // PerRendererData：纹理由 Image/SpriteRenderer 提供，不显示在材质面板
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        // 扰动参数
        _NoiseScale ("Noise Scale (扰动密度)", Range(1, 40)) = 7
        _DistortStrength ("Distort Strength (扰动强度 UV)", Range(0, 0.2)) = 0.015
        _FlowSpeed ("Flow Speed (流动速度)", Range(0, 5)) = 0.6
        _FlowDir ("Flow Direction (流动方向 xy)", Vector) = (0.5, -0.3, 0, 0)
        [Toggle(_SHIMMER_ON)] _ShimmerOn ("Enable Shimmer (明暗流动)", Float) = 1
        _Shimmer ("Shimmer Intensity (明暗流动强度)", Range(0, 0.5)) = 0.1

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
            #pragma shader_feature_local _SHIMMER_ON

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
                // 噪声域坐标：uv 缩放 + 时间平移在顶点阶段完成（线性运算）
                float2 noiseUV       : TEXCOORD2;
                // 世界坐标，供 _ClipRect 矩形裁剪使用
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            // UGUI 全局：图集边缘扩张色与矩形裁剪框，由 CanvasRenderer 注入
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _NoiseScale;
            float _DistortStrength;
            float _FlowSpeed;
            float4 _FlowDir;
            float _Shimmer;

            // Simplex 2D 噪声（Ashima Arts），输出约 [-1, 1]
            float3 mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float2 mod289(float2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float3 permute(float3 x) { return mod289(((x * 34.0) + 1.0) * x); }

            float snoise(float2 v)
            {
                const float4 C = float4(0.211324865405187, 0.366025403784439,
                                       -0.577350269189626, 0.024390243902439);
                float2 i  = floor(v + dot(v, C.yy));
                float2 x0 = v - i + dot(i, C.xx);
                float2 i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
                float4 x12 = x0.xyxy + C.xxzz;
                x12.xy -= i1;
                i = mod289(i);
                float3 p = permute(permute(i.y + float3(0.0, i1.y, 1.0))
                                 + i.x + float3(0.0, i1.x, 1.0));
                float3 m = max(0.5 - float3(dot(x0, x0), dot(x12.xy, x12.xy),
                                             dot(x12.zw, x12.zw)), 0.0);
                m = m * m;
                m = m * m;
                float3 x = 2.0 * frac(p * C.www) - 1.0;
                float3 h = abs(x) - 0.5;
                float3 ox = floor(x + 0.5);
                float3 a0 = x - ox;
                m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);
                float3 g;
                g.x  = a0.x * x0.y - h.x * x0.x;
                g.yz = a0.yz * x12.xz - h.yz * x12.yw;
                return 130.0 * dot(m, g);
            }

            // 双通道 fbm：x/y 各 2 层（大尺度形变 + 细节），偏移采样点去相关，
            // 避免 x/y 偏移同向导致画面整体平移而非扭动
            half2 Fbm2(float2 p)
            {
                float nx = snoise(p) * 0.65 + snoise(p * 2.17 + 5.2) * 0.35;
                float ny = snoise(p + float2(17.3, 9.1)) * 0.65
                         + snoise(p * 2.17 + float2(31.7, 23.4)) * 0.35;
                return half2(nx, ny);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                // 噪声域：密度缩放 + 沿流动方向的匀速漂移
                OUT.noiseUV = v.texcoord * _NoiseScale
                            + _Time.y * _FlowSpeed * normalize(_FlowDir.xy + 1e-4);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 流动噪声偏移场：把 UV 推出去，实现图片内容扭动
                half2 n = Fbm2(IN.noiseUV);
                float2 uv = IN.texcoord + n * _DistortStrength;

                // 基础 UI 颜色：扰动采样 + 全局扩张色 × 顶点色 × Tint
                half4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;

                #ifdef _SHIMMER_ON
                // 明暗流动：复用 x 通道噪声做亮度调制，产生水面反光质感
                color.rgb *= 1.0 + n.x * _Shimmer;
                #endif

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
