// 无光照流动表面：UV 随时间偏移循环流动，xy 速度独立控制，自发光叠加，透明混合
// 依赖贴图 Wrap Mode = Repeat（默认）实现无缝循环；同一采样复用为底色与自发光来源
Shader "Effect/Flow Surface"
{
    Properties
    {
        _BaseMap("流动贴图", 2D) = "white" {}
        _BaseColor("主色", Color) = (1,1,1,1)
        //UV 每秒流动量（正负均可：负值反向流动）
        _FlowSpeedX("X轴流动速度", Float) = 0
        _FlowSpeedY("Y轴流动速度", Float) = 1
        [HDR] _EmissionColor("自发光颜色", Color) = (1,1,1,1)
        _EmissionIntensity("自发光强度", Range(0, 5)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 0

        Pass
        {
            Name "ForwardUnlit"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseMap_ST;
            half4 _BaseColor;
            float _FlowSpeedX;
            float _FlowSpeedY;
            half4 _EmissionColor;
            float _EmissionIntensity;
            CBUFFER_END

            sampler2D _BaseMap;

            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float4 clipPosition : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            VertexOutput vert(VertexInput v)
            {
                VertexOutput o;
                o.clipPosition = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(VertexOutput o) : SV_Target
            {
                //时间偏移流动：_Time.y 为秒计时间，xy 速度独立控制，偏移量 = 速度 × 时间
                half2 flowUV = o.uv + half2(_FlowSpeedX, _FlowSpeedY) * _Time.y;
                half4 texSample = tex2D(_BaseMap, flowUV);

                //底色 + 自发光：同一采样复用（只采一次）；自发光不经过主色调制，颜色为 HDR 供 Bloom 提取
                half3 finalColor = texSample.rgb * _BaseColor.rgb;
                finalColor += texSample.rgb * _EmissionColor.rgb * _EmissionIntensity;

                return half4(finalColor, texSample.a * _BaseColor.a);
            }

            ENDHLSL
        }
    }
    Fallback "Hidden/InternalErrorShader"
}
