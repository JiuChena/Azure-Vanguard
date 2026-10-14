---
tags:
  - graphics
  - rendering
  - theory
created: 2026-06-20
---

# 计算机图形学

## 渲染管线 (Rendering Pipeline)

渲染管线是 CPU 与 GPU 协作将 3D 场景转换为 2D 图像的完整流程。

### 管线全景

```
CPU 应用阶段 → GPU 几何处理 → 光栅化 → 片元处理 → 后处理
```

### 第一阶段：CPU 应用阶段

- CPU 准备渲染数据（场景裁剪、可见性判断）
- 发起 Draw Call，将数据提交给 GPU
- 设置渲染状态（材质、着色器、混合模式）

### 第二阶段：GPU 几何处理

```
顶点着色器 → 曲面细分（可选）→ 几何着色器（可选）→ 裁剪 → 屏幕映射
```

- **顶点着色器 (Vertex Shader)**: 处理每个顶点，执行坐标变换（模型→世界→观察→裁剪空间）
- **曲面细分 (Tessellation)**: 动态增加三角形密度，实现 LOD 效果
- **几何着色器 (Geometry Shader)**: 对图元进行操作，生成新顶点
- **裁剪 (Clipping)**: 移除视锥体外的图元
- **屏幕映射**: 将裁剪坐标映射到屏幕坐标

### 第三阶段：GPU 光栅化

```
三角形设置 → 三角形遍历 → 片元着色器
```

- **三角形设置**: 从顶点数据计算三角形边方程
- **三角形遍历**: 确定哪些像素被三角形覆盖，生成片元 (Fragment)
- 应用遮挡剔除（Early-Z / Pre-Z）在此阶段可拒绝被遮挡的片元

### 第四阶段：GPU 片元处理

```
片元着色器 → 逐片元操作（深度测试、模板测试、混合）
```

- **片元着色器 (Fragment / Pixel Shader)**: 计算每个片元的最终颜色（光照、纹理采样等）
- **深度测试 (Z-Test)**: 比较片元深度与 Z-Buffer，决定是否可见
- **模板测试 (Stencil Test)**: 基于模板缓冲进行像素级过滤
- **混合 (Blending)**: 将新片元颜色与已有颜色按规则混合（用于透明效果）

### Alpha Test 与 Early-Z

- **Alpha Test**: 在片元着色器中根据 Alpha 值丢弃片元，阻止 Early-Z 优化
- **Early-Z / Pre-Z**: 在片元着色器之前执行深度测试，提前拒绝被遮挡片元，减少无效计算

### Z-Fighting

当两个面距离过近且深度缓冲精度不足时，画面出现闪烁条纹。解决方案：增大 Near/Far 距离比、使用更高精度深度格式、偏移物体位置。

---

## 光照模型

### Blinn-Phong 光照模型

由三个分量组成，计算效率高，是实时渲染中最经典的光照模型。

```
最终颜色 = 环境光 (Ambient) + 漫反射 (Diffuse) + 高光 (Specular)
```

#### 环境光 (Ambient)

模拟间接光照，防止完全没有光照时物体纯黑。

```hlsl
float3 ambient = _AmbientLight.rgb * albedo;
```

#### 漫反射 (Diffuse)

Lambert 漫反射定律，与观察角度无关。

```hlsl
float NdotL = max(0, dot(normal, lightDir));
float3 diffuse = _LightColor.rgb * albedo * NdotL;
```

#### 高光 (Specular)

使用半角向量 (Half Vector) 而非反射向量，效率更高。

```hlsl
float3 halfVec = normalize(lightDir + viewDir); // 半角向量
float spec = pow(max(0, dot(normal, halfVec)), _Gloss);
float3 specular = _LightColor.rgb * _SpecularColor * spec;
```

### Phong vs Blinn-Phong

| 特性 | Phong | Blinn-Phong |
|------|-------|-------------|
| 高光计算 | 反射向量 R 与视线 V 的点积 | 半角向量 H 与法线 N 的点积 |
| 性能 | 需计算反射向量 | 需计算半角向量（更快） |
| 效果 | 高光更锐利 | 高光更柔和、范围更大 |

---

## PBR (Physically Based Rendering)

基于物理的渲染，模拟真实世界的光照行为。

### 核心原则

1. **能量守恒**: 反射光能量不能超过入射光能量
2. **微表面理论**: 表面由无数微小镜面组成，用粗糙度 (Roughness) 描述其分布
3. **菲涅尔效应 (Fresnel)**: 视线越平行于表面，反射越强

### 核心参数

| 参数 | 说明 | Unity 中的属性 |
|------|------|---------------|
| **Albedo** | 基础颜色（不包含光照信息） | `_BaseColor` |
| **Metallic** | 金属度（金属或非金属） | `_Metallic` |
| **Smoothness / Roughness** | 光滑度/粗糙度 | `_Smoothness` |
| **Normal Map** | 法线贴图（增加表面细节） | `_BumpMap` |
| **Ambient Occlusion** | 环境光遮蔽 | `_OcclusionMap` |

### Unity 中的 PBR

- **Standard Shader**（内置管线）: Unity 默认 PBR 着色器
- **URP Lit Shader**: URP 中的 PBR
- **HDRP Lit Shader**: HDRP 中的高级 PBR

---

## Light Map (光照贴图)

将静态光照信息预计算并烘焙到纹理中，运行时直接采样，极大节省实时光照开销。

### 工作流程

1. 将物体标记为 **Static**
2. 在 Lighting 窗口中设置环境光、方向光参数
3. 点击 **Generate Lighting** 进行烘焙
4. 运行时静态物体直接从 Light Map 采样光照，无需实时计算

### 优缺点

- **优点**: 性能极佳，光照质量高（支持全局光照 GI）
- **缺点**: 只支持静态物体，烘焙耗时，增大包体和内存

---

## Shadow Map (阴影贴图)

从光源视角渲染场景的深度图，在主渲染时判断每个片元是否在阴影中。

### 基本原理

1. 从光源位置渲染场景，将深度信息存入 Shadow Map（只写深度，不计算颜色）
2. 在正常渲染时，将每个片元变换到光源空间
3. 比较片元深度与 Shadow Map 中对应位置的深度
4. 如果片元深度 > Shadow Map 深度，则该片元在阴影中

### 常见问题及解决

| 问题 | 原因 | 解决方案 |
|------|------|----------|
| **Shadow Acne** (阴影痤疮) | 深度精度误差导致自阴影 | 使用 Depth Bias / Normal Bias |
| **Peter Panning** (彼得潘) | Bias 过大导致阴影与物体分离 | 减小 Bias |
| **锯齿** (Aliasing) | Shadow Map 分辨率不足 | 增大分辨率、使用 PCF 软阴影滤波 |
| **透视走样** | 近处精度高、远处精度低 | CSM (Cascaded Shadow Maps) |

### CSM (Cascaded Shadow Maps)

将摄像机视锥体分为多个级联区域，每个区域使用独立的 Shadow Map，近处高分辨率、远处低分辨率。

---

## 空间加速结构

### BVH (Bounding Volume Hierarchy)

包围体层次结构，是射线追踪和碰撞检测中常用的空间加速结构：
- 将场景物体递归地用包围盒（AABB）包裹
- 形成树形结构，叶子节点为实际几何体
- 射线检测时先测试父包围盒，仅当相交时才深入子节点
- 大幅减少不必要的相交测试

### 其他空间结构

| 结构 | 特点 |
|------|------|
| **Octree** | 八叉树，递归八等分空间 |
| **BSP** | 二叉空间分割，常用于室内渲染的可见性判断 |
| **KD-Tree** | K 维空间分割树 |
| **Grid / Uniform Grid** | 均匀网格，适合均匀分布的物体 |

---

## 抗锯齿技术

| 技术 | 原理 | 性能影响 | 效果 |
|------|------|----------|------|
| **MSAA** | 对每个像素采样多次（硬件支持） | 中等 | 几何边缘平滑 |
| **FXAA** | 后处理检测高对比度边缘进行模糊 | 低 | 整体柔化，细节可能模糊 |
| **TAA** | 利用历史帧信息进行时域累积 | 中等 | 效果好，可配合运动向量 |
| **SMAA** | 边缘检测 + 形态学抗锯齿 | 中等 | 接近 MSAA 效果，性能更好 |

---

## 射线追踪概念 (Ray Tracing)

模拟光线在场景中的真实传播路径，产生物理正确的反射、折射、阴影。

### 核心概念

- **光线与场景求交**: 利用 BVH 等加速结构高效计算
- **路径追踪 (Path Tracing)**: 追踪光线从摄像机出发，经过多次弹射到达光源
- **蒙特卡洛积分**: 通过大量随机采样近似求解光照积分

### Unity 中的 Ray Tracing

- HDRP 支持 DXR（DirectX Raytracing）硬件加速
- 用于高质量反射、阴影、全局光照
- 需要支持的硬件（NVIDIA RTX 系列等）

---

## GPU Skin 渲染

将骨骼动画的蒙皮计算放在 GPU 着色器中执行，减轻 CPU 负担。Unity 默认在 GPU 上进行骨骼蒙皮（GPU Skinning），在大规模角色渲染时优势明显。

---

## Forward Rendering vs Deferred Rendering

| 特性 | Forward Rendering | Deferred Rendering |
|------|-------------------|---------------------|
| **原理** | 每个物体逐光源计算光照，几何→着色一步完成 | 先将几何信息写入 G-Buffer，再屏幕空间逐光源计算 |
| **多光源** | 每增加一个光源，draw call 和 shader 复杂度线性增长 | 光源数量对性能影响较小，适合大量动态光源 |
| **透明物体** | 原生支持，按深度排序后渲染即可 | 不支持（G-Buffer 无法存储多层透明信息），需额外 Forward Pass |
| **移动端** | 适合，带宽开销低，支持 MSAA | 不适合，G-Buffer 带宽消耗大（MRT），移动 GPU 带宽敏感 |
| **MSAA** | 支持硬件 MSAA | 不支持（MRT 下硬件 MSAA 受限），需后处理抗锯齿（TAA/FXAA） |
| **典型管线** | 内置管线、URP Forward 模式 | HDRP、URP Deferred 模式 |

## 后处理效果

- **Bloom**: 先提取画面高亮区域（亮度阈值过滤），再对高亮部分做多次高斯模糊（降采样+升采样），最后与原图叠加。模拟相机过曝溢出效果
- **SSAO (屏幕空间环境光遮蔽)**: 在屏幕空间中采样像素周围深度，估算局部遮蔽因子，暗化缝隙和角落。开销低但只考虑屏幕可见几何
- **Motion Blur**: 利用运动矢量（上一帧到当前帧的像素位移）对画面做定向模糊。模拟快速运动或摄像机旋转时的拖影感
- **Tone Mapping (色调映射)**: 将 HDR 高动态范围颜色映射到 LDR 显示范围（0-1）。常见算法：ACES（电影级）、Filmic、Reinhard。防止高光过曝死白

## MipMap

预先生成纹理的缩小版本链（每级分辨率减半：512→256→128→...）。渲染时根据像素在屏幕上的实际大小选择合适 Mip 级别：
- 远处/倾斜面用低 Mip 级，减少纹理锯齿（Moire 纹），提升视觉质量
- 提高 GPU 纹理缓存命中率（相邻像素采样同一低 Mip 级别），减少带宽
- 额外内存开销约 33%（所有 Mip 级总和的等比数列）。Unity 导入纹理时勾选 **Generate Mip Maps** 即可启用

## LOD (Level of Detail)

根据物体与摄像机的距离动态切换模型精度，远距离使用低面数模型以降低 GPU 顶点负载：
- Unity 使用 **LOD Group** 组件，配置不同距离阈值及对应的 Mesh 渲染器
- 一般设置 3 级（LOD0/1/2）+ Culled（超出最远距离不渲染）
- 跨平台基本一致，只是移动端可能需要更激进的距离阈值（更早切换到低模），以降低顶点和带宽压力

## Compute Shader

在 GPU 上执行通用并行计算，不直接参与渲染管线：
- **典型应用**: 粒子系统模拟（数万粒子运动轨迹）、布料/头发物理、GPU 剔除、后处理加速、体素生成
- 跨平台支持：HLSL 编写，编译为 DX/OpenGL/Metal/Vulkan 对应格式
- 结果可写回 CPU 内存（ComputeBuffer）或直接作为渲染输入（Graphics.DrawProcedural）
