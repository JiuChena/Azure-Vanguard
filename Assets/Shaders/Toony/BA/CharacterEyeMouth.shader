Shader "GTS/BACharacterEyeMouth"
{
    Properties
    {
        _EyeTexture("眼睛贴图", 2D) = "white" {}
        _MouthTexture("嘴部贴图", 2D) = "white" {}
        _EyeMouthMask("眼睛贴图遮罩", 2D) = "black" {}
        _MouthSize("xy行列数 | zw单张嘴部贴图尺寸", Vector) = (8, 8, 4, 4)
        _EyeBrightness("眼睛亮度", Range(1, 50)) = 2
        _MouthIndex("嘴部贴图索引", Float) = 1
        _Cutoff("Alpha裁剪阈值", Range(0, 1)) = 0.5

        [Space(8)]
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendSrc("混合源(BlendSrc)", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendDst("混合目标(BlendDst)", Float) = 10
        _Transparency("透明度(最终Alpha)", Range(0, 1)) = 1

        _DiffuseSteps("嘴巴色阶数", Range(2, 50)) = 3
        _DiffuseSmooth("嘴巴色阶柔化", Range(0, 1)) = 0.2
        _DiffuseWrap("嘴巴漫反射包裹", Range(0, 1)) = 0
        _HColor("嘴巴亮面色", Color) = (1,1,1,1)
        _ShadowColor("嘴巴阴影色", Color) = (0.68,0.68,0.68,1)
        _MainLightDiffuseScale("嘴巴主光强度", Range(0, 5)) = 1
        _IndirectlightScale("嘴巴间接光强度", Range(0, 1)) = 0.4

        _EyeLightScale("眼睛受光影响Scale", Range(0, 5)) = 1
        _EyeLightMax("眼睛受光提亮上限", Range(0, 10)) = 2

        _ParallaxCenter("视差中心", Vector) = (0.5, 0.5, 0, 0)
        _ParallaxScale("视差强度", Range(0, 1)) = 0.03
        _ParallaxMaskEdge("视差半径", Range(0, 1)) = 0.4
        _ParallaxMaskEdgeOffset("视差边缘柔化", Range(0, 1)) = 0.1
        _ParallaxEllipse("视差椭圆缩放(X,Y)", Vector) = (1, 1, 0, 0)

        [Toggle(_DEBUG_EYELIGHT_ON)] _DebugEyeLight("调试:显示眼睛提亮度", Float) = 0
        [Toggle(_DEBUG_PARALLAX_ON)] _DebugParallax("调试:显示视差范围", Float) = 0

        [Enum(Off, Write, Read)] _StencilMode("模板测试模式", Float) = 0
        _StencilRef("模板值", Range(0, 255)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilCompare("模板比较", Float) = 6
        [HideInInspector] _StencilForwardComp("Stencil Forward Comp", Float) = 8
        [HideInInspector] _StencilForwardOp("Stencil Forward Op", Float) = 2
    }
    SubShader
    {
        // Geometry-10：先于头发(Geometry)渲染并写入模板标记，供头发模板测试读取（眉毛透过头发组装）
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="Geometry-10" }
        LOD 100

        //模板测试（前向门控）：写入模式=标记缓冲区；读取模式=比较通过才渲染（不过不画）；关闭=中立
        Stencil { Ref [_StencilRef] Comp [_StencilForwardComp] Pass [_StencilForwardOp] }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend [_BlendSrc] [_BlendDst]

            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _DEBUG_EYELIGHT_ON
            #pragma shader_feature_local _DEBUG_PARALLAX_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
            
            half4 _EyeTexture_ST;
            half4 _MouthTexture_ST;
            half4 _EyeMouthMask_ST;
            float _EyeBrightness;
            half4 _MouthSize;
            float _MouthIndex;
            half _Cutoff;

            half _DiffuseSteps;
            half _DiffuseSmooth;
            half _DiffuseWrap;
            float4 _HColor;
            float4 _ShadowColor;
            float _MainLightDiffuseScale;
            float _IndirectlightScale;

            float _EyeLightScale;
            float _EyeLightMax;
            half4 _ParallaxCenter;
            float _ParallaxScale;
            float _ParallaxMaskEdge;
            float _ParallaxMaskEdgeOffset;
            half4 _ParallaxEllipse;
            half _DebugEyeLight;
            half _DebugParallax;
            float _BlendSrc;
            float _BlendDst;
            float _Transparency;

            CBUFFER_END
            
            sampler2D _EyeTexture;
            sampler2D _MouthTexture;
            sampler2D _EyeMouthMask;

            struct VertexInput
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 uv : TEXCOORD0;
            };

            struct VertextOutput
            {
                float4 clipPosition : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPosition : TEXCOORD2;
            };
            
            float2 FlipbookUV(float2 uv, float2 size, float tileIndex)
            {
                float2 tileCount = size;
                float index = floor(tileIndex);
                
                float row = floor(index / tileCount.y);
                float col = index - row * tileCount.y;
                
                float2 cell = 1 / tileCount;
                float2 baseUV = uv * cell;
                baseUV.x += col * cell.x;
                baseUV.y += (tileCount.y - row - 1) * cell.y;
                
                return baseUV;
            }
            
            VertextOutput vert(VertexInput v)
            {
                VertextOutput o = (VertextOutput)0;
                
                o.worldPosition = TransformObjectToWorld(v.vertex.xyz);
                o.clipPosition = TransformWorldToHClip(o.worldPosition);
                o.uv.xy = v.uv.xy;
                o.worldNormal = TransformObjectToWorldNormal(v.normal);
                
                return o;
            }
            
            half4 frag(VertextOutput o) : SV_Target
            {
                // 眼睛视差采样：视线方向→模型空间 XY 作 UV 偏移（凹面瞳孔立体感）
                float3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - o.worldPosition);
                float3 viewDirOS = TransformWorldToObjectDir(viewDirWS);
                viewDirOS = normalize(viewDirOS);
                float2 parallaxOffset = viewDirOS.xy;
                parallaxOffset.y *= -1;   // UV Y 轴翻转
                float2 parallaxUV = o.uv + _ParallaxScale * parallaxOffset;

                // 视差遮罩：以参数化中心为圆心的椭圆半径内生效（XY 缩放→椭圆）
                float2 centerVec = o.uv - _ParallaxCenter.xy;
                half2 scaledVec = centerVec / max(_ParallaxEllipse.xy, 0.0001);
                half centerDist = length(scaledVec);
                half parallaxMask = 1 - smoothstep(_ParallaxMaskEdge, _ParallaxMaskEdge + _ParallaxMaskEdgeOffset, centerDist);

                // 眼部贴图采样（按遮罩混合：中心用视差 UV，外围用原 UV）
                half4 eyeSample = tex2D(_EyeTexture, lerp(o.uv, parallaxUV, parallaxMask));
                half4 eyeColor = eyeSample * _EyeBrightness;

                //嘴部图片位置计算并采样
                float2 mouthUV = o.uv * _MouthSize.zw;
                mouthUV = FlipbookUV(mouthUV, _MouthSize.xy, _MouthIndex);
                half4 mouthColor = tex2D(_MouthTexture, mouthUV);

                //遮罩贴图采样
                half mask = tex2D(_EyeMouthMask, o.uv).r;

                //主光漫反射计算
                half4 shadowCoords = TransformWorldToShadowCoord(o.worldPosition);
                Light mainLight = GetMainLight(shadowCoords);
                half lightShadowAttenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                half NL = dot(o.worldNormal, mainLight.direction);
                half wrapNL = lerp(max(0, NL), (NL + 1) * 0.5, _DiffuseWrap);

                //色阶量化
                half steps = max(round(_DiffuseSteps), 2);
                half bandPos = wrapNL * (steps - 1);
                half bandIdx = floor(bandPos);
                half bandFrac = frac(bandPos);
                half bandBlend = smoothstep(max(1.0 - _DiffuseSmooth, 0.0001), 1.0, bandFrac);
                half rampStep = saturate((bandIdx + bandBlend) / (steps - 1));
                rampStep *= lightShadowAttenuation;

                //附加光漫反射计算
                half3 additionalDiffuse = 0;
                #if defined(_ADDITIONAL_LIGHTS)
                for (int i = 0; i < GetAdditionalLightsCount(); i++)
                {
                    Light light = GetAdditionalLight(i, o.worldPosition);
                    half ndl = saturate(dot(o.worldNormal, light.direction));
                    additionalDiffuse += ndl * light.shadowAttenuation * light.distanceAttenuation * light.color;
                }
                #endif

                //全局光照计算
                half3 bakedGI = SampleSH(o.worldNormal);
                MixRealtimeAndBakedGI(mainLight, o.worldNormal, bakedGI);
                half3 ambient = lerp(half3(0,0,0), bakedGI, _IndirectlightScale);

                //阴影强度计算：眼睛强度趋近于0、嘴部强度不趋近于0
                half finalA = lerp(eyeSample.a, mouthColor.a, mask);
                half shadeA = finalA - (1 - mask);

                //漫反射组装并由shadeA控制阴影强度
                half shadowIntensity = saturate(shadeA) * _ShadowColor.a;
                half3 shadowColorMixed = lerp(_HColor.rgb, _ShadowColor.rgb, shadowIntensity);
                half3 mainDiffuse = lerp(shadowColorMixed, _HColor.rgb, rampStep) * _MainLightColor.rgb * _MainLightDiffuseScale;
                half3 mouthLit = (mainDiffuse + additionalDiffuse + ambient) * mouthColor.rgb;

                //眼睛整体亮度
                half mainLightBrightness = saturate(wrapNL) * lightShadowAttenuation;
                half additionalBrightness = dot(additionalDiffuse, half3(0.2126, 0.7152, 0.0722));
                half giBrightness = dot(ambient, half3(0.2126, 0.7152, 0.0722));
                half overallLight = mainLightBrightness + additionalBrightness + giBrightness;

                // Scale 乘受光调节参数控制提亮影响程度，Max 只作提亮上限截断（不影响中间值）
                half eyeLightBoost = saturate(overallLight) * _EyeLightScale;
                half eyeLight = 1 + min(eyeLightBoost, _EyeLightMax);
                half3 eyeLit = eyeColor.rgb * eyeLight;

                //色彩混合
                half3 finalRGB = lerp(eyeLit, mouthLit, mask);
                // 最终输出Alpha：由 _Transparency 控制混合透明度（配合 _BlendSrc/_BlendDst 半透明）
                half4 finalColor = half4(finalRGB, _Transparency);

                // Alpha裁剪：基于眼睛/嘴部遮罩alpha，与最终混合透明度独立
                clip(finalA - _Cutoff);

                // 调试：只显示眼睛区域的提亮度灰度（mask≈0 是眼睛区），嘴巴区域保持正常
                #ifdef _DEBUG_EYELIGHT_ON
                    half debugGrey = saturate((eyeLight - 1) / max(_EyeLightMax, 0.0001));
                    finalColor.rgb = lerp(debugGrey.xxx, finalColor.rgb, mask);
                #endif

                // 调试：显示视差范围（中心黑点 + 椭圆半径线），仅眼睛区域
                #ifdef _DEBUG_PARALLAX_ON
                    float2 dbgCenterVec = o.uv - _ParallaxCenter.xy;
                    half2 dbgScaled = dbgCenterVec / max(_ParallaxEllipse.xy, 0.0001);
                    half dbgDist = length(dbgScaled);
                    // 中心黑点
                    half dotMark = 1 - smoothstep(0.0, 0.015, dbgDist);
                    // 椭圆半径线（在 _ParallaxMaskEdge 处画线，宽度 0.008）
                    half ringMark = 1 - smoothstep(0.0, 0.008, abs(dbgDist - _ParallaxMaskEdge));
                    half mark = max(dotMark, ringMark);
                    // mask≈0 是眼睛区，只在眼睛区显示标记
                    finalColor.rgb = lerp(finalColor.rgb, 0, mark * (1 - mask));
                #endif

                return finalColor;
            }
            
            ENDHLSL
        }
    }
    CustomEditor "CharacterEyeMouthEditor"
}
