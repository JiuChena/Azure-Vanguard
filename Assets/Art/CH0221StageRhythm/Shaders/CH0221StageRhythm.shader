// 二次元舞台律动：独立地板、中心向外扩散的色环、竖直音乐栅栏。
Shader "AzureVanguard/VFX/CH0221 Stage Rhythm"
{
    Properties
    {
        [Header(Rhythm)]
        _BPM("节拍速度 BPM", Range(30, 240)) = 120
        _Phase("节拍起始偏移 秒", Float) = 0
        _Opacity("整体不透明度", Range(0, 1)) = 1
        _Intensity("发光强度", Range(0, 3)) = 1.15
        [Enum(Floor,0,Wave,1,Fence,2)] _Layer("特效层", Float) = 0
        [Header(Stage)]
        _GridCount("直径方向格子数量", Range(8, 56)) = 30
        _GridStrength("格阵亮度", Range(0, 1)) = 0.52
        _GridFill("方格填充比例", Range(0.3, 0.9)) = 0.58
        _GridRadius("方格限制半径 比例", Range(0.05, 1)) = 1
        _FloorRingRadius("地板光圈半径 比例", Range(0.05, 0.985)) = 0.95
        _CenterCover("中心色块半径", Range(0.1, 0.65)) = 0.4
        _RingWidth("扩散波半宽比例", Range(0.005, 0.08)) = 0.035
        _WaveBeats("扩散一轮拍数", Range(1, 8)) = 4
        _WaveStrength("扩散波亮度", Range(0, 1)) = 0.75
        _FenceHeight("律动条最大高度 米", Range(0.1, 2)) = 0.65
        [Header(Colors)]
        [HDR] _GoldColor("地板细圈颜色", Color) = (1, 0.88, 0.08, 1)
        _GridColor("地面格阵暖色", Color) = (1, 0.72, 0.23, 1)
        _CoolColor("边缘格阵冷色", Color) = (0.38, 0.92, 1, 1)
        [Header(SourceTextures)]
        _NoiseTex("CH0221 波纹扰动", 2D) = "gray" {}
        _EdgeTex("Trail 波形边缘", 2D) = "white" {}
        _PaletteTex("彩虹渐变", 2D) = "white" {}
        [HideInInspector] _PreviewTime("编辑器定格时间 负值为实时", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "StageRhythm"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_EdgeTex); SAMPLER(sampler_EdgeTex);
            TEXTURE2D(_PaletteTex); SAMPLER(sampler_PaletteTex);
            CBUFFER_START(UnityPerMaterial)
                float _BPM, _Phase, _Opacity, _Intensity, _Layer;
                float _GridCount, _GridStrength, _GridFill, _CenterCover;
                float _GridRadius, _FloorRingRadius;
                float _RingWidth, _WaveStrength, _WaveBeats, _FenceHeight;
                half4 _GoldColor, _GridColor, _CoolColor;
                float _PreviewTime;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // 每根条带仅变形顶部，不在 CPU 上逐帧更新网格。
                if (_Layer > 1.5)
                {
                    float time = (_PreviewTime >= 0 ? _PreviewTime : _Time.y) + _Phase;
                    float beat = time * _BPM / 60.0;
                    float bar = floor(input.uv.x * 128.0);
                    float height = 0.22 + 0.48 * pow(0.5 + 0.5 * sin(bar * 0.67 - beat * PI), 2.0);
                    height += 0.3 * pow(0.5 + 0.5 * sin(bar * 0.19 + beat * TWO_PI), 3.0);
                    input.positionOS.y *= height * _FenceHeight;
                }
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // 基于屏幕导数抗锯齿，避免斜视格子和细圆环闪烁。
            float Ring(float radius, float center, float width, float aa)
            {
                return 1.0 - smoothstep(width - aa, width + aa, abs(radius - center));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = (_PreviewTime >= 0 ? _PreviewTime : _Time.y) + _Phase;
                float beat = time * _BPM / 60.0;
                half3 color = 0;
                float alpha = 0;

                // 外围只有分段的立体音乐条，不添加固定的地面范围圈。
                if (_Layer > 1.5)
                {
                    float segment = frac(input.uv.y * 6.0);
                    float edgeAA = max(fwidth(input.uv.y * 6.0), 0.015);
                    float tiles = smoothstep(0.1, 0.1 + edgeAA, segment) * (1.0 - smoothstep(0.76 - edgeAA, 0.76, segment));
                    half3 palette = SAMPLE_TEXTURE2D(_PaletteTex, sampler_PaletteTex, float2(frac(input.uv.x + input.uv.y * 0.15 - time * 0.13), 0.5)).rgb;
                    alpha = tiles * (0.65 + 0.2 * sin(beat * PI + input.uv.x * TWO_PI));
                    color = palette * alpha * 1.3;
                }
                else
                {
                    float2 p = (input.uv - 0.5) * 2.0;
                    float r = length(p);
                    clip(1.0 - r);
                    float aa = max(fwidth(r), 0.0004);
                    if (_Layer > 0.5)
                    {
                        // 两道宽波从中心出发，在最后 18% 路程快速收细并淡出。
                        float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, frac(input.uv * 1.6 + time * float2(0.025, -0.018))).r;
                        float angle = atan2(p.y, p.x) / TWO_PI + 0.5;
                        float edge = SAMPLE_TEXTURE2D(_EdgeTex, sampler_EdgeTex, float2(frac(angle * 12.0 - time * 0.05), 0.47)).r;
                        half3 palette = SAMPLE_TEXTURE2D(_PaletteTex, sampler_PaletteTex, float2(frac(r - beat / _WaveBeats + time * 0.12 + 0.15), 0.5)).rgb;
                        [unroll] for (int wave = 0; wave < 2; wave++)
                        {
                            float phase = frac(beat / max(_WaveBeats, 1.0) + wave * 0.5);
                            float radius = phase * 0.985;
                            float endFade = 1.0 - smoothstep(0.82, 1.0, phase);
                            float birthFade = smoothstep(0.0, 0.055, phase);
                            float width = min(_RingWidth, radius * 0.6) * endFade;
                            float disturbed = r + ((noise - 0.5) * 0.003 + (edge - 0.5) * 0.002) * endFade;
                            float band = Ring(disturbed, radius, width, aa);
                            float halo = exp2(-abs(r - radius) * 130.0) * 0.1;
                            float strength = birthFade * endFade * _WaveStrength;
                            color += palette * (band * 1.25 + halo) * strength;
                            alpha += (band * 0.9 + halo * 0.2) * strength;
                        }
                    }
                    else
                    {
                        // 中心覆盖成连续色块；外部格子保持间距，边界以宽区间平滑渐隐。
                        float cover = 1.0 - smoothstep(_CenterCover * 0.72, _CenterCover * 1.35, r);
                        // 仅缩放方格遮罩半径，保持格子尺寸、间距和光圈半径相互独立。
                        float gridRadius = r / max(_GridRadius, 0.01);
                        float fade = 1.0 - smoothstep(0.45, 0.99, gridRadius);
                        float2 cellUV = input.uv * _GridCount;
                        float2 cell = abs(frac(cellUV) - 0.5);
                        float cellAA = max(max(fwidth(cellUV.x), fwidth(cellUV.y)), 0.01);
                        float square = 1.0 - smoothstep(_GridFill * 0.5 - cellAA, _GridFill * 0.5 + cellAA, max(cell.x, cell.y));
                        float pulse = 0.87 + 0.13 * sin(beat * PI - r * 7.0);
                        float warm = 1.0 - smoothstep(0.3, 0.95, r);
                        half3 tint = lerp(_CoolColor.rgb, _GridColor.rgb, warm);
                        tint = lerp(tint, half3(1, 1, 1), (1.0 - warm) * 0.4);
                        float grid = square * (1.0 - cover) * fade * _GridStrength * pulse;
                        float wash = cover * (0.42 + 0.035 * sin(beat * PI));
                        wash *= 1.0 - smoothstep(0.85, 1.0, gridRadius);
                        color = tint * grid + _GridColor.rgb * wash;
                        alpha = grid * 0.65 + wash * 0.7;
                        float border = Ring(r, _FloorRingRadius, 0.0026, aa);
                        color += _GoldColor.rgb * border * 0.8;
                        alpha = saturate(alpha + border * 0.7);
                    }
                    float borderFade = 1.0 - smoothstep(0.99, 1.0, r);
                    color *= borderFade;
                    alpha *= borderFade;
                }
                clip(alpha - 0.001);
                return half4(color * _Intensity * _Opacity, saturate(alpha) * _Opacity);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
