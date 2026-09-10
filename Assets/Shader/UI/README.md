# UI Shader 目录说明

UI 特效 shader 统一放本目录，按效果类型分子文件夹；shader 菜单路径统一用 `AzureVanguard/UI/...` 前缀。

## 目录结构

```text
Assets/Shader/UI/
├── Dissolve/          # 溶解类
│   └── UIDissolve.shader      # 噪声溶解（UGUI，纯 Alpha 溶解）
└── README.md          # 本文件
```

新增效果类别时按"效果族"建文件夹（如 Blur、Transition、Outline），一种效果族一个文件夹。

## UIDissolve（噪声溶解）使用说明

1. 创建材质：右键 → Create → Material，Shader 选 `AzureVanguard/UI/DissolveNoise`，赋给目标 Image。
2. 指定噪声图：`Noise Texture` 用灰度噪声图（Wrap Mode 建议 Repeat）；噪声图决定溶解的形状分布。
3. 驱动溶解：运行时脚本改材质数值（UI 材质 Unity 会自动实例化，不影响其他同图 Image）：

```csharp
// value: 0（完整显示）→ 1（完全消失）
image.material.SetFloat("_DissolveAmount", value);
```

4. 行为约定：

- 以 Image 组件的 Sprite 为主；溶解**只作用于 alpha**，不混合、不修改 RGB
- 进度 0 = 完整显示；进度 1 = 完全消失，无残留（纯白噪声像素也已处理）

5. 参数速查：

| 参数 | 作用 |
| --- | --- |
| Noise Scale | 噪声密度，越大溶解碎块越细 |
| Dissolve Amount | 溶解进度，0 完整 / 1 全消 |

6. 兼容性：支持 Mask / RectMask2D、UI 顶点色、Sprite 图集；URP 下自动走 SRPDefaultUnlit。
