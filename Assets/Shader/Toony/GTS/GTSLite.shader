// 简化版 GTS：最经济的场景物体卡通渲染（仅主光两阶明暗 + 环境光），带描边变体
// 属性名与 URP Lit 对齐（_BaseMap/_BaseColor/_BumpMap/_BumpScale/_Cull）：
// 在已有 Lit 材质上直接换 shader 时同名属性自动保留，贴图数据不丢失
// 需要遮罩/高光/边缘光/头发高光等完整功能时用同目录 GeneralToonyShader
Shader "GTS/General Toony Shader Lite"
{
    Properties
    {
        _BaseMap("主纹理图", 2D) = "white" {}
        _BaseColor("主色", Color) = (1,1,1,1)
        _BumpMap("法线贴图", 2D) = "bump" {}
        _BumpScale("法线强度", Float) = 1

        _MainLightDiffuseScale("主光漫反射强度", Range(0, 5)) = 1
        //两阶明暗：NL 高于分界为亮部色，低于为阴影色；过渡带宽度由柔化控制
        _ToonThreshold("明暗分界", Range(0, 1)) = 0.5
        _ToonSmoothness("明暗过渡柔化", Range(0.001, 0.5)) = 0.1
        _ShadowColor("阴影色(乘在底色上)", Color) = (0.65,0.65,0.8,1)
        _AmbientScale("环境光强度", Range(0, 2)) = 1

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("剔除模式", Float) = 2

        [Toggle(_OUTLINE_ON)] _UseOutline("描边", Float) = 1
        _OutlineColor("描边颜色", Color) = (0,0,0,1)
        //世界空间法线外推宽度（0.01 为单位换算系数；固定宽度，需距离自适应用完整版）
        _OutlineWidth("描边宽度", Range(0, 2)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 0

        //描边通道（无 LightMode 标签 = SRPDefaultUnlit，前向默认渲染；开关关闭时顶点塌缩不产生像素）
        Pass
        {
            Name "Outline"

            Cull Front

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _OUTLINE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            //两通道 CBUFFER 布局保持一致（SRP Batcher 要求）
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _BumpMap_ST;
            float _BumpScale;
            float _MainLightDiffuseScale;
            float _ToonThreshold;
            float _ToonSmoothness;
            half4 _ShadowColor;
            float _AmbientScale;
            float _Cull;
            float _UseOutline;
            half4 _OutlineColor;
            float _OutlineWidth;
            CBUFFER_END

            struct VertexInput
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct VertexOutput
            {
                float4 clipPosition : SV_POSITION;
            };

            VertexOutput vert(VertexInput v)
            {
                VertexOutput o;
#ifdef _OUTLINE_ON
                //世界空间法线外推（反向壳）：沿法线挤出后再以正面剔除渲染轮廓
                float3 worldNormal = TransformObjectToWorldNormal(v.normal);
                float3 worldPosition = TransformObjectToWorld(v.vertex.xyz);
                worldPosition += worldNormal * (0.01 * _OutlineWidth);
                o.clipPosition = TransformWorldToHClip(worldPosition);
#else
                o.clipPosition = float4(0, 0, 0, 1);
#endif
                return o;
            }

            half4 frag(VertexOutput o) : SV_Target
            {
                return _OutlineColor;
            }

            ENDHLSL
        }

        //前向渲染通道：仅主光两阶明暗卡通漫反射 + 环境光，无高光/边缘光/附加光
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Cull [_Cull]

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            //主光实时阴影（接收投影）；_SHADOWS_SOFT 控软阴影衰减
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            //两通道 CBUFFER 布局保持一致（SRP Batcher 要求）
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _BumpMap_ST;
            float _BumpScale;
            float _MainLightDiffuseScale;
            float _ToonThreshold;
            float _ToonSmoothness;
            half4 _ShadowColor;
            float _AmbientScale;
            float _Cull;
            float _UseOutline;
            half4 _OutlineColor;
            float _OutlineWidth;
            CBUFFER_END

            sampler2D _BaseMap;
            sampler2D _BumpMap;

            struct VertexInput
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float4 clipPosition : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldTangent : TEXCOORD2;
                float3 worldBitangent : TEXCOORD3;
                float2 uv : TEXCOORD4;
            };

            VertexOutput vert(VertexInput v)
            {
                VertexOutput o = (VertexOutput)0;

                //世界空间 TBN（法线贴图用）与位置
                o.worldNormal = TransformObjectToWorldNormal(v.normal);
                o.worldTangent = TransformObjectToWorldDir(v.tangent.xyz);
                float tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                o.worldBitangent = cross(o.worldNormal, o.worldTangent) * tangentSign;
                o.worldPosition = TransformObjectToWorld(v.vertex.xyz);
                o.clipPosition = TransformWorldToHClip(o.worldPosition);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);

                return o;
            }

            half4 frag(VertexOutput o) : SV_Target
            {
                //法线贴图（无切线网格时 TBN 退化，与完整版 GTS 一致的限制）
                half3 tangentNormal = UnpackNormalScale(tex2D(_BumpMap, o.uv), _BumpScale);
                half3x3 TBN = half3x3(o.worldTangent, o.worldBitangent, o.worldNormal);
                half3 worldNormal = SafeNormalize(TransformTangentToWorld(tangentNormal, TBN));

                //主光与实时阴影衰减
                float4 shadowCoord = TransformWorldToShadowCoord(o.worldPosition);
                Light mainLight = GetMainLight(shadowCoord);

                //两阶明暗：NL 过分界为亮部、低于为阴影部；过渡带对称覆盖分界，柔化控制带宽
                half NL = dot(worldNormal, mainLight.direction);
                half halfBand = max(_ToonSmoothness * 0.5, 0.0001);
                half ramp = smoothstep(_ToonThreshold - halfBand, _ToonThreshold + halfBand, NL);
                //实时阴影并入色阶：被遮挡像素直接落入阴影档（与完整版 GTS 的 rampStep *= 衰减同思路）
                ramp *= mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                //底色
                half3 baseColor = tex2D(_BaseMap, o.uv).rgb * _BaseColor.rgb;

                //漫反射：阴影色与原色按色阶插值，再乘主光颜色
                half3 diffuse = lerp(_ShadowColor.rgb, half3(1, 1, 1), ramp)
                              * mainLight.color * _MainLightDiffuseScale;

                //环境光（光照探针/球谐）：阴影部不至于纯黑
                half3 ambient = SampleSH(worldNormal) * _AmbientScale;

                return half4(baseColor * (diffuse + ambient), 1);
            }

            ENDHLSL
        }

        //阴影投射与深度写入
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    Fallback "Hidden/InternalErrorShader"
}
