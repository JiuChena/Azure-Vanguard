---
tags: [unity, rendering, graphics, shader]
created: 2026-06-20
---

# Unity 渲染与图形

## 1. Draw Call 与合批（Batching）

### 什么是 Draw Call

Draw Call 是 CPU 向 GPU 发起的一次渲染指令。每次 Draw Call 通知 GPU 使用哪些网格、材质、Shader 来绘制一批物体。

- **Draw Call 数量过高**：CPU 瓶颈，CPU 花费大量时间在渲染命令提交上
- **优化目标**：减少 Draw Call 数量，将多个物体合并为一次渲染

### 静态合批（Static Batching）

- **原理**：将标记为 `Static` 的物体（使用相同材质）合并为一个大 Mesh 进行渲染
- **条件**：物体需勾选 Inspector 中的 `Static` 标志
- **内存代价**：会在运行时创建合并后的 Mesh，增加内存占用
- **适用**：场景中不移动的建筑物、地形、道具

```csharp
// 在运行时手动标记静态（注意：该操作不可逆）
gameObject.isStatic = true;
```

### 动态合批（Dynamic Batching）

- **原理**：Unity 自动将顶点数较少的动态物体（使用相同材质）合并渲染
- **条件**：顶点数 < 300（实际限制因 Unity 版本和平台而异），使用相同材质
- **限制**：不支持多 Pass 的 Shader、不支持 SkinnedMeshRenderer、缩放差异过大的物体无法合批
- **适用**：大量小物件（子弹、粒子、道具）

### GPU Instancing

- **原理**：使用相同 Mesh 和 Material 的多个实例，GPU 自动批量绘制，减少 Draw Call
- **使用方式**：Material 勾选 `Enable GPU Instancing`
- **Shader 要求**：Shader 需支持 Instancing（`#pragma multi_compile_instancing`）
- **适用**：大量重复物体（树木、石头、敌人）

```hlsl
// Shader 中支持 GPU Instancing
Shader "Custom/Instanced"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            // ...
            ENDCG
        }
    }
}
```

### SRP Batcher

- **原理**：Scriptable Render Pipeline (URP/HDRP) 的合批机制，将相同 Shader Variant 的材质数据缓存在 GPU
- **优点**：不受顶点数限制，不消耗额外内存
- **要求**：使用 URP 或 HDRP，Shader 兼容 SRP Batcher
- **与动态合批的区别**：SRP Batcher 不合并 Mesh，而是优化材质属性的更新方式

---

## 2. 渲染管线

### 基本概念

渲染管线是将 3D 场景数据转化为 2D 屏幕像素的一系列步骤。Unity 中的渲染管线主要分三个阶段：

#### 阶段一：应用阶段（CPU）

- 由 CPU 主导，准备渲染数据
- 剔除（Frustum Culling、Occlusion Culling）：确定哪些物体可见
- 排序（Sorting）：按材质、深度排序以优化渲染顺序
- 设置渲染状态（Render State Setup）
- 发起 Draw Call，将数据发送给 GPU

#### 阶段二：几何阶段（GPU）

- **顶点着色器（Vertex Shader）**：对每个顶点执行坐标变换（模型空间 → 世界空间 → 观察空间 → 裁剪空间）
- **曲面细分着色器（Tessellation Shader）**：可选阶段，增加几何体细节
- **几何着色器（Geometry Shader）**：可选，生成/销毁图元
- **裁剪（Clipping）**：裁剪掉视锥体外的图元
- **屏幕映射**：将裁剪空间坐标映射到屏幕空间

#### 阶段三：光栅化阶段（GPU）

- **三角形设置 & 遍历**：确定三角形覆盖了哪些像素
- **Early-Z / Pre-Z**：在片段着色器之前进行深度测试，提前剔除被遮挡的片段
- **片段着色器（Fragment Shader）**：对每个像素计算最终颜色（光照、纹理采样、阴影等）
- **逐片元操作**：深度测试（Z-Test）、模板测试（Stencil Test）、混合（Blending）

### Forward 渲染 vs Deferred 渲染

| 特性 | Forward Rendering | Deferred Rendering |
|------|-------------------|---------------------|
| 光照计算 | 每个物体 × 每盏灯 | 延迟到全屏 Pass，只算可见像素 |
| 多灯光性能 | 灯光数 × 物体数 | 灯光数 × 屏幕像素（通常更优） |
| 抗锯齿 | 支持 MSAA | 不支持原生 MSAA |
| 透明物体 | 自然支持 | 需额外 Forward Pass |
| 内存带宽 | 低 | 高（GBuffer 多个 RT） |
| 适用 | 移动端、VR、少灯光场景 | PC/主机、多灯光场景 |

**Forward+**（Forward Plus）是 Forward 的改进：先做深度 Pass，建立 Tile-based 灯光列表，减少不必要的灯光计算。

---

## 3. UGUI Canvas 渲染模式

UGUI 的 Canvas 组件有三种渲染模式，决定了 UI 如何显示以及与其他对象的遮挡关系。

### Screen Space - Overlay（屏幕空间 - 覆盖）

- UI 直接渲染到屏幕最上层，不需要相机
- **特点**：始终覆盖在所有 3D 物体之上
- **相关属性**：`Pixel Perfect`（像素完美）、`Sort Order`（排序层级）、`Target Display`（目标显示器）
- **适用**：传统 HUD、主菜单、弹窗

### Screen Space - Camera（屏幕空间 - 相机）

- UI 绑定到指定相机，作为相机视野的一部分渲染
- **特点**：3D 物体可以遮挡 UI（通过深度），UI 可以受相机后处理影响
- **相关属性**：`Render Camera`（渲染相机）、`Plane Distance`（平面距离）、`Sorting Layer / Order in Layer`
- **适用**：需要 3D 物体遮挡 UI 或相机特效的场景

### World Space（世界空间）

- UI 作为 3D 世界中的对象存在，如同一个 3D 平面
- **特点**：UI 可以像 3D 物体一样旋转、缩放、被遮挡，需要 `EventCamera` 接收交互事件
- **相关属性**：`Event Camera`（事件相机）、世界空间缩放
- **适用**：VR/AR UI、游戏内 3D 指示器、场景中的交互面板

### 排序规则

- **Overlay 模式**：通过 `Sort Order` 设置层级
- **Camera / World Space 模式**：通过 `Sorting Layer` 和 `Order in Layer` 控制
- **同一 Canvas 内**：子物体的渲染顺序由其 `Sibling Index`（Hierarchy 中的顺序）决定

### Canvas 模式切换示例

```csharp
using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class CanvasRenderModeSwitcher : MonoBehaviour
{
    [Header("相机引用")]
    public Camera uiCamera;      // Screen Space - Camera 模式使用
    public Camera eventCamera;   // World Space 模式的事件相机

    [Header("模式开关（仅用于测试）")]
    public bool useOverlay = true;
    public bool useCamera = false;
    public bool useWorldSpace = false;

    [Header("World Space 参数")]
    public float worldSpaceScale = 2f;

    private Canvas _canvas;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        if (useOverlay) SetOverlay();
        else if (useCamera) SetCameraMode(uiCamera);
        else if (useWorldSpace) SetWorldSpace(eventCamera, worldSpaceScale);
    }

    void SetOverlay()
    {
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.pixelPerfect = true;
        _canvas.sortingOrder = 10;
    }

    void SetCameraMode(Camera cam)
    {
        _canvas.renderMode = RenderMode.ScreenSpaceCamera;
        _canvas.worldCamera = cam;
        _canvas.planeDistance = 100f;
    }

    void SetWorldSpace(Camera eventCam, float scale)
    {
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = eventCam;
        var rect = _canvas.GetComponent<RectTransform>();
        rect.localScale = Vector3.one * scale;
    }
}
```

> 注意：以上代码从 OCR 中还原。OCR 原文写得较为详细，但存在部分变量名 OCR 误差，已修正。

---

## 4. UI 裁剪（Mask 与 RectMask2D）

### RectMask2D（推荐优先使用）

- **原理**：基于矩形区域的裁剪，通过修改子对象顶点实现
- **优点**：不额外增加 Draw Call，性能好
- **限制**：只能是矩形裁剪区域，不能做圆角或不规则形状

### Mask（基于 Stencil 模板测试）

- **原理**：利用 GPU 的模板缓冲（Stencil Buffer），Mask 写入模板值，子对象读取模板值决定是否渲染
- **特点**：支持不规则形状裁剪（配合 Image 的 sprite 形状）
- **缺点**：每个 Mask 会产生额外的 Draw Call（至少 2 次），性能开销较大
- **适用**：需要非矩形裁剪的场景

### ⚠️ 使用注意事项

- `RectMask2D` 比 `Mask` 性能更优，优先使用
- `Mask` 使用 Stencil，层级嵌套越多 Draw Call 越多
- `ScrollView` 的 `Viewport` 默认使用 `RectMask2D`
- 所有 `Graphic` 子类（Image、Text、RawImage 等）皆可被 Mask/RectMask2D 裁剪
- 需要 `RectTransform` 来定义裁剪区域
- 对于复杂形状裁剪（如圆形头像），可结合 `Image` 的圆形 sprite + Mask，或使用 Shader 实现

> ⚠️ OCR 原文对 Mask 和 RectMask2D 的区别描述较为清晰。以上整理结合了 OCR 内容和标准知识补充。关于 3D 场景中的遮罩方案（Stencil Shader / RenderTexture），OCR 有提及但细节不完整。

---

## 5. UGUI 布局组件（Layout Groups）

### 常用布局组件

- **Horizontal Layout Group**：水平排列子物体
- **Vertical Layout Group**：垂直排列子物体
- **Grid Layout Group**：网格排列子物体
- **Content Size Fitter**：根据内容自动调整 RectTransform 大小
- **Aspect Ratio Fitter**：固定宽高比
- **Layout Element**：为布局中的单个元素设置最小/首选/灵活尺寸

### 布局计算流程

Unity UGUI 每一帧的布局计算顺序：
1. 先计算子物体的 `LayoutElement` 的 min/preferred/flexible 尺寸
2. `LayoutGroup` 根据子物体尺寸分配空间
3. `ContentSizeFitter` 根据子物体总尺寸调整自身
4. 最终更新 `RectTransform`

### 性能注意事项

- 嵌套多层 Layout Group 会触发多次布局重计算（Layout Rebuild）
- 频繁修改布局子物体（如列表滚动）建议使用对象池 + 手动布局
- `Canvas.ForceUpdateCanvases()` 可强制立即更新（调试用，不宜常态调用）

> ⚠️ OCR 材料未详细展开 Layout Groups 的具体实现。以上为根据面试考点补充的标准 UGUI 布局知识。

---

## 6. 相机投影模式

### Perspective（透视投影）

- **原理**：模拟人眼或真实相机，近大远小，有消失点
- **参数**：
  - `Field of View (FOV)`：视野角度，控制视野范围
  - `Aspect`：宽高比
  - `Near / Far Clipping Plane`：近/远裁剪面
- **适用**：3D 游戏主相机、第一/三人称视角

### Orthographic（正交投影）

- **原理**：无近大远小效果，物体大小与距离无关
- **参数**：
  - `Orthographic Size`：视野大小的半高度（单位：世界空间单位）
  - `Near / Far Clipping Plane`：近/远裁剪面
- **适用**：2D 游戏、策略游戏、小地图、编辑器视角

### 投影切换要点

```csharp
// Inspector 中修改
Camera cam = GetComponent<Camera>();
cam.orthographic = true;  // 切换为正交
cam.orthographicSize = 5f; // 设置视野大小

cam.orthographic = false; // 切换为透视
cam.fieldOfView = 60f;    // 设置 FOV

// 近似换算关系
// OrthographicSize ≈ (FOV * 0.5) / Aspect（粗略估算，不精确）
```

### 面试常见问题

- 透视和正交各自适合什么场景？
- FOV、Near/Far 如何影响深度精度？
- 如何在运行时平滑切换投影模式？（建议使用 `Mathf.Lerp` 过渡参数）

---

## 7. URP / HDRP / Built-in 管线对比

### Built-in Render Pipeline（内置管线）

- Unity 默认的传统渲染管线
- 功能全面但配置灵活性有限
- 大量 Shader 基于旧版 Surface Shader

### URP（Universal Render Pipeline，通用渲染管线）

- 基于 SRP（Scriptable Render Pipeline），可自由定制
- **优点**：轻量、跨平台、性能优化（SRP Batcher）
- **适用**：移动端、VR、中低端 PC、多数跨平台项目
- **特性**：支持 Shader Graph、单 Pass 前向渲染、RenderFeature 自定义后处理

### HDRP（High Definition Render Pipeline，高清渲染管线）

- 面向高端平台（PC、主机）
- **优点**：照片级画质、物理光照、体积光、光线追踪
- **适用**：3A 级主机/PC 游戏、建筑可视化、影视
- **特性**：延迟渲染 + 前向渲染混合、体积雾、PBR 物理材质、SSR、SSGI

### 对比总结

| 特性 | Built-in | URP | HDRP |
|------|----------|-----|------|
| 画质上限 | 中等 | 良好 | 极高（Cinematic） |
| 性能 | 一般 | 优秀 | 仅限高端硬件 |
| 可定制性 | 低 | 高 | 高 |
| 跨平台 | 全平台 | 全平台（含移动端） | PC / Console |
| Shader Graph | 不支持 | 支持 | 支持 |
| SRP Batcher | 不支持 | 支持 | 支持 |

> ⚠️ OCR 原文中关于 URP/HDRP 的条目较少，仅以关键词形式出现。以上对比为标准化补充。

---

## 8. Shader 基础

### 什么是 Shader

Shader 是运行在 GPU 上的小程序，决定顶点如何变换以及像素如何着色。

### Shader 类型

- **Vertex Shader（顶点着色器）**：处理顶点数据，执行坐标变换
- **Fragment/Pixel Shader（片段着色器）**：计算每个像素的颜色
- **Geometry Shader**：处理图元级别操作（可选）
- **Compute Shader**：在 GPU 上执行通用并行计算

### Unity Shader 结构

```hlsl
Shader "Custom/MyShader"
{
    Properties
    {
        _MainTex ("主纹理", 2D) = "white" {}
        _Color ("颜色", Color) = (1,1,1,1)
        _Gloss ("光滑度", Range(0,1)) = 0.5
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Gloss;
            CBUFFER_END
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return texColor * _Color;
            }
            ENDHLSL
        }
    }
}
```

### Unity Shader 常见概念

- **Surface Shader**：Unity 传统简化写法，自动生成 Vertex/Fragment Shader（Built-in 管线）
- **Shader Graph**：可视化 Shader 编辑器，适用于 URP/HDRP
- **Shader Variant**：通过 `#pragma multi_compile` 生成的多个 Shader 变体，变体爆炸会导致构建时间增加

---

## 9. Blinn-Phong 光照模型

### 基本组成

```
最终颜色 = 环境光(Ambient) + 漫反射(Diffuse) + 高光(Specular)
```

#### 漫反射（Diffuse） — Lambert 模型

```
Diffuse = LightColor * MaterialDiffuse * max(0, dot(N, L))
```
- N：表面法线
- L：指向光源的方向

#### 高光（Specular） — Blinn-Phong

```
HalfVector H = normalize(L + V)
Specular = LightColor * MaterialSpecular * pow(max(0, dot(N, H)), Gloss)
```
- V：指向观察者的方向
- H：半角向量（Half Vector）
- Gloss：光泽度（Shininess），值越大高光越集中

### Blinn-Phong vs Phong

- **Phong**：使用反射向量 R，计算 `pow(dot(R, V), gloss)`
- **Blinn-Phong**：使用半角向量 H，计算 `pow(dot(N, H), gloss)`
- Blinn-Phong 计算效率更高，视觉效果通常也更自然

---

## 10. Lightmap 与 Shadow Map

### Lightmap（光照贴图）

将静态光照信息预计算并烘焙到纹理上，运行时直接采样。

- **优点**：运行时无光照计算开销，可呈现复杂的全局光照效果
- **缺点**：仅适用于静态物体（标记为 Static），无法响应动态光源变化，占用纹理内存
- **使用**：`Window > Rendering > Lighting` 中配置烘焙参数

### Shadow Map（阴影贴图）

从光源视角渲染深度图，运行时比较深度判断阴影。

- **原理**：
  1. 从光源位置渲染场景深度到 Shadow Map
  2. 从相机渲染时，将每个像素变换到光源空间
  3. 比较当前像素深度与 Shadow Map 中存储的深度
  4. 深度更大的像素处于阴影中
- **常见问题**：
  - **Shadow Acne（阴影痤疮）**：深度精度不足导致的自阴影条纹 → 使用 **Shadow Bias（偏移）** 修正
  - **Peter Panning（阴影悬浮）**：Bias 过大导致阴影与物体分离
  - **Z-Fighting**：两个面深度值过于接近导致闪烁，可通过调整 Near/Far 平面缓解

> ⚠️ OCR 原文对 Shadow Map 和 Shadow Acne／Bias 有提及，但具体算法推导不够完整。以上为标准知识补充。

---

## 11. 遮挡剔除（Occlusion Culling）

### 原理

预先计算场景中哪些物体在特定相机位置下会被其他物体遮挡，运行时跳过被遮挡物体的渲染。

### Unity 实现

- 在 `Window > Rendering > Occlusion Culling` 中烘焙遮挡数据
- 物体需要标记为 `Occluder Static`（遮挡物）或 `Occludee Static`（被遮挡物）
- 运行时 Unity 自动根据相机位置查询可见性

### Umbra（内置遮挡剔除方案）

- Unity 内置使用 Umbra 进行遮挡剔除
- 基于预计算的可见性数据（PVS + Portal）

### Early-Z / Pre-Z

- 在片段着色器之前执行深度测试
- 如果片段深度测试失败，直接丢弃，不执行代价高昂的 Fragment Shader
- 需要从前往后（Opaque: Front to Back）渲染，配合深度写入开启
- GPU 硬件原生支持，无需额外设置（但需要正确的渲染顺序）

---

## 12. LOD（细节层次）

### 原理

根据物体与相机的距离，切换不同精度的模型（高面数 → 低面数），减少顶点处理压力。

### Unity 中的 LOD Group

```csharp
// 通过脚本控制 LOD（通常由 LOD Group 组件自动处理）
LODGroup lodGroup = GetComponent<LODGroup>();
LOD[] lods = lodGroup.GetLODs();
// lods[0] = 高精度（近距离）
// lods[1] = 中精度（中距离）
// lods[2] = 低精度（远距离）
// 最后一个 LOD 的 Renderers 可为空数组（Culled，完全剔除）
```

### LOD 配置要点

- LOD0：最高精度，相机最近时使用
- LOD1-LODN：逐渐降低精度
- 可配置 Culled 距离，超出最远距离直接不渲染
- 防止 LOD 切换时的视觉跳变（Dithering 过渡 / 平滑 LOD 过渡）

---

## 13. 材质与纹理管理

### Material（材质）

材质是 Shader 的实例化配置，包含 Shader 引用和属性值（颜色、纹理引用、数值参数等）。

- **MaterialPropertyBlock**：替换材质属性而不创建新材质实例，适用于 GPU Instancing
- **SharedMaterial vs Material**：`renderer.sharedMaterial` 修改会应用到所有共享该材质的物体；`renderer.material` 会创建独立实例

```csharp
// 使用 MaterialPropertyBlock 避免创建新材质实例
MaterialPropertyBlock mpb = new MaterialPropertyBlock();
mpb.SetColor("_Color", Color.red);
renderer.SetPropertyBlock(mpb);
```

### Texture（纹理）

- **MipMap**：贴图的缩小版本序列，用于远处物体渲染时使用低分辨率版本，减少闪烁和带宽
- **压缩格式**：
  - 移动端：ASTC（通用）、ETC2（Android）、PVRTC（iOS 旧设备）
  - PC/主机：DXT/BC 系列
- **纹理图集（Texture Atlas / Sprite Atlas）**：将多个小纹理合并为一张大纹理，减少 Draw Call
- **MipMap** 用途：减少远处物体纹理闪烁（Moire 效果），提升 Cache 命中率

### 纹理内存估算

```
RGBA32 1024×1024 = 1024 × 1024 × 4 bytes = 4 MB
包含 MipMap：约 4 × 1.33 = 5.33 MB
带 ASTC 6×6 压缩：约 4 / (6*6/16) ≈ 1.78 MB
```

> ⚠️ OCR 材料中对于 MipMap 有简单提及。纹理压缩及内存计算为标准化补充。

---

## 14. 其他图形学相关概念

### Alpha Test vs Alpha Blend

- **Alpha Test**：片段着色器中，如果 alpha 小于阈值直接丢弃（`clip(alpha - 0.5)`），不写入深度，不支持半透明过渡（要么全透明要么全不透明）
- **Alpha Blend**：根据 alpha 值将当前颜色与背景混合，实现真正的半透明效果，通常需要关闭深度写入

### PBR（Physically Based Rendering，基于物理的渲染）

基于物理的光照模型，模拟真实世界材质的光照行为，使用 BRDF（双向反射分布函数）描述材质如何反射光线。

- Unity URP/HDRP 默认使用 PBR 管线
- 核心参数：Albedo（反照率）、Metallic（金属度）、Smoothness（光滑度）、Normal Map（法线贴图）

### BRDF

BRDF（Bidirectional Reflectance Distribution Function，双向反射分布函数）描述从某个方向入射的光线在表面向各个方向反射的比例。不同材质的 BRDF 不同（金属、塑料、皮肤等）。

### GPU Skinning

GPU 端执行骨骼蒙皮计算（而非 CPU），减少 CPU 负载。Unity 中通过 Player Settings 开启 `GPU Skinning`。

### 渲染顺序与深度测试

Unity 标准渲染顺序：
1. **不透明物体（Opaque）**：从前往后（Front to Back），深度写入开启，深度测试为 Less/Equal
2. **天空盒（Skybox）**：在不透明物体之后渲染
3. **透明物体（Transparent）**：从后往前（Back to Front），深度写入关闭，深度测试为 Less/Equal
4. 不同 RenderQueue：
   - Background = 1000
   - Geometry = 2000
   - AlphaTest = 2450
   - Transparent = 3000
   - Overlay = 4000

### Z-Fighting

两个面深度值相近导致交替显示，产生闪烁现象。解决方案：
- 增加 Near/Far 裁剪面比（提高深度缓冲精度）
- 调整物体间距离
- 使用 Offset（深度偏移）

---

## 面试常见问题总结

根据 OCR 材料中面试记录的归纳：

1. **Draw Call 是什么？如何减少 Draw Call？**（静态合批、动态合批、GPU Instancing、SRP Batcher）
2. **GPU Instancing 的原理与使用条件**
3. **UGUI Canvas 的三种 Render Mode 及其使用场景**
4. **RectMask2D 和 Mask 的区别及使用注意事项**
5. **相机投影模式有哪些？各适合什么场景？**
6. **URP 和 HDRP 的区别**
7. **Shader 的顶点着色器和片段着色器分别做什么？**
8. **Lightmap 和 Shadow Map 的原理**
9. **什么是遮挡剔除？Early-Z 是什么？**
10. **LOD 系统的原理和 Unity 实现**
11. **渲染管线三个阶段（应用阶段、几何阶段、光栅化阶段）**
12. **Forward 渲染与 Deferred 渲染的对比**
13. **Blinn-Phong 光照模型的组成**
14. **什么是 PBR？BRDF 描述了什么？**
15. **MipMap 的作用和原理**
16. **Z-Fighting 产生原因和解决办法**

> ⚠️ OCR 原文中部分图形学问题（如光栅化管线阶段、PBR/BRDF 详细原理）只有简短关键词，以上补充了标准化知识。如有面试需要建议深入阅读《Real-Time Rendering》及相关资料。
