---
tags:
  - unity
  - physics
created: 2026-06-20
---

# Unity 物理系统

## 概述

Unity 物理系统基于 NVIDIA PhysX 引擎（3D），提供刚体、碰撞检测、关节、射线检测等功能。物理计算在 **FixedUpdate** 中以固定时间步长执行，保证模拟的一致性。

---

## Rigidbody (刚体)

Rigidbody 是 Unity 物理引擎的核心组件，使 GameObject 受物理系统控制。

### 关键属性

| 属性 | 说明 |
|------|------|
| **Mass** | 质量，影响碰撞响应 |
| **Drag** | 平移阻力（空气阻力），减慢移动 |
| **Angular Drag** | 角阻力，减慢旋转 |
| **Use Gravity** | 是否受重力影响 |
| **Is Kinematic** | 运动学刚体，不受力影响（由代码驱动移动），可与动态物体碰撞 |
| **Collision Detection** | 碰撞检测模式（Discrete / Continuous / Continuous Dynamic） |
| **Interpolate / Extrapolate** | 平滑运动抖动的插值模式 |
| **Constraints** | 冻结位置/旋转轴 |

### 基本用法

```csharp
// 获取或添加 Rigidbody
Rigidbody rb = GetComponent<Rigidbody>();
rb.mass = 1.0f;
rb.drag = 0.5f;
rb.useGravity = true;

// 施加力（应在 FixedUpdate 中执行）
void FixedUpdate()
{
    rb.AddForce(Vector3.forward * 100f);          // 持续力
    rb.AddForce(Vector3.up * 500f, ForceMode.Impulse); // 瞬间力
    rb.AddExplosionForce(1000f, explosionPos, 10f);    // 爆炸力
}
```

### Rigidbody 类型

| 类型 | 特征 | 用法 |
|------|------|------|
| **Dynamic** | 完全受物理引擎驱动，受力/重力/碰撞影响 | 玩家、敌人、可推动物体 |
| **Kinematic** | 不受力影响，代码控制（MovePosition/MoveRotation），可与 Dynamic 碰撞 | 平台、电梯、移动机关 |
| **Static** | 不受物理影响，不移动（通常无 Rigidbody，仅用 Collider + Static 标记） | 墙壁、地面 |

### 最佳实践

- 避免直接修改 Rigidbody 的 Transform，使用 `MovePosition` / `MoveRotation`
- 物理力操作在 `FixedUpdate` 中执行
- 修改速度（`velocity`）可以直接在 Update 中操作

---

## Collider 类型

根据模型形状选择合适的碰撞体以平衡精度和性能。

| Collider 类型 | 形状 | 性能 | 适用场景 |
|--------------|------|------|----------|
| **Box Collider** | 盒子 | 最优 | 方形物体、墙壁、地板 |
| **Sphere Collider** | 球体 | 最优 | 球、角色粗略碰撞 |
| **Capsule Collider** | 胶囊 | 较好 | 角色/人形物体 |
| **Mesh Collider** | 任意网格 | 最差 | 复杂静态场景（需 Convex 才能碰撞其他 Mesh Collider） |
| **Wheel Collider** | 车轮专用 | 中等 | 载具 |

### Compound Collider

使用多个原始碰撞体组合模拟复杂形状，性能优于 Mesh Collider。

### 物理材质 (Physic Material)

```csharp
// 创建物理材质
PhysicMaterial mat = new PhysicMaterial();
mat.bounciness = 0.5f;      // 弹性 (0-1)
mat.dynamicFriction = 0.6f;  // 动摩擦
mat.staticFriction = 0.6f;   // 静摩擦
mat.frictionCombine = PhysicMaterialCombine.Average;
mat.bounceCombine = PhysicMaterialCombine.Maximum;
GetComponent<Collider>().material = mat;
```

---

## 碰撞检测模式

| 模式 | 检测方式 | 性能开销 | 适用场景 |
|------|----------|----------|----------|
| **Discrete** | 每固定帧检测一次 | 最低 | 速度慢的物体 |
| **Continuous** | 连续扫掠检测 (CCD) | 中等 | 快速移动物体防穿透 |
| **Continuous Dynamic** | 对所有参与碰撞体使用 CCD | 最高 | 多个快速物体交互 |
| **Continuous Speculative** | 预测性连续检测 | 中等 | 2D 快速物体的替代方案 |

### 穿透问题 (Tunneling)

物体速度过快导致在两帧之间穿过薄的碰撞体：
- **方案1**: 使用 Continuous / Continuous Dynamic 碰撞检测
- **方案2**: 增大碰撞体厚度
- **方案3**: 使用 Raycast 辅助检测（Sweep Test）
- **方案4**: 降低 `Time.fixedDeltaTime`（增加物理步进频率）

---

## Trigger 触发器

Trigger 是 Is Trigger 勾选的 Collider，不产生物理碰撞反馈，仅检测进入/离开区域。

### 回调和区别

```csharp
void OnTriggerEnter(Collider other)  // 进入触发区域
{
    // 应用：拾取道具、进入触发剧情区域
}

void OnTriggerStay(Collider other)   // 持续在触发区域内
{
    // 应用：持续伤害区域
}

void OnTriggerExit(Collider other)   // 离开触发区域
{
    // 应用：离开安全区域
}
```

### Trigger vs Collision 对照

| 特性 | Trigger (`IsTrigger = true`) | Collision (`IsTrigger = false`) |
|------|------------------------------|--------------------------------|
| 物理碰撞反馈 | 无（穿透） | 有（弹开/停止） |
| 回调函数 | `OnTriggerEnter/Stay/Exit` | `OnCollisionEnter/Stay/Exit` |
| 典型用途 | 检测区域、拾取物品、触发剧情 | 墙壁阻挡、物理推撞 |
| 需要 Rigidbody | 至少一方有 | 双方有 Collider，至少一方有 Rigidbody |

---

## 射线检测 (Raycasting)

从某点沿方向发射不可见射线，检测与 Collider 的相交。

```csharp
void Update()
{
    if (Input.GetMouseButtonDown(0))
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Debug.Log($"命中: {hit.collider.name}, 点: {hit.point}");
            // hit.normal  : 碰撞面法线
            // hit.distance: 射线起点到碰撞点的距离
        }
    }
}

// 带 LayerMask 过滤
int layerMask = LayerMask.GetMask("Enemy", "Ground");
if (Physics.Raycast(ray, out hit, 100f, layerMask)) { }
```

### 常用射线 API

| API | 说明 |
|-----|------|
| `Physics.Raycast(origin, dir, out hit, maxDist)` | 基本射线，遇第一个碰撞体停止 |
| `Physics.RaycastAll(origin, dir, maxDist)` | 返回所有命中的碰撞体 |
| `Physics.SphereCast(origin, radius, dir, out hit)` | 球形投射（更宽松） |
| `Physics.BoxCast(center, halfExtents, dir, out hit)` | 盒形投射 |
| `Physics.Linecast(start, end, out hit)` | 两点间线段 |

### 典型应用

- 鼠标点击选中物体
- 射击/弹道检测
- 地面检测（判断是否在地面上）
- AI 视线检测（是否能看到目标）
- 摄像机墙壁避让

### 性能注意

- 始终使用 **LayerMask** 过滤不必要的碰撞层
- `RaycastAll` 比 `Raycast` 开销更大
- 大量射线检测可使用 `RaycastCommand`（Jobs System 支持）

---

## Joints (关节)

连接两个 Rigidbody，限制相对运动。

### 常见关节类型

| 关节 | 说明 | 典型应用 |
|------|------|----------|
| **Fixed Joint** | 刚性固定连接 | 组合物体 |
| **Spring Joint** | 弹簧般的弹性连接 | 弹簧、橡皮筋 |
| **Hinge Joint** | 铰链，绕一个轴旋转 | 门、钟摆 |
| **Character Joint** | 高级关节，用于人形角色 | 布娃娃 (Ragdoll) |
| **Configurable Joint** | 功能最全面的可配置关节 | 自定义物理约束 |

---

## Update vs FixedUpdate 与物理

| 函数 | 调用频率 | 适用场景 |
|------|----------|----------|
| **Update** | 每帧一次（帧率相关） | 常规逻辑、输入检测 |
| **FixedUpdate** | 固定时间片（默认 0.02s ≈ 50次/秒） | 物理计算、Rigidbody 力操作 |
| **LateUpdate** | Update 之后 | 摄像机跟随 |

### 关键注意事项

- `AddForce` 等物理操作 **必须在 FixedUpdate 中**
- 直接设置 `velocity` 可以在 Update 中
- 读取输入（`GetKeyDown` 等）放在 Update，在 FixedUpdate 中使用缓存的输入值
- 可通过 `Edit > Project Settings > Time > Fixed Timestep` 调整物理步进频率
- 使用 `Physics.autoSimulation = false` 可手动控制物理步进

---

## 物理系统优化

- 静态物体标记为 **Static** 减少物理开销
- 使用 **Layer Collision Matrix**（`Edit > Project Settings > Physics`）禁用不必要的碰撞
- 合理调整 `Solver Iterations` 和 `Sleep Threshold`
- 使用 Compound Collider 替代 Mesh Collider

---

## 2D 物理系统

2D 物理基于 **Box2D** 引擎，与 3D 物理（PhysX）API 风格类似但完全独立：
- **Rigidbody2D** + **Collider2D 系列**（BoxCollider2D、CircleCollider2D、CapsuleCollider2D、PolygonCollider2D、EdgeCollider2D）
- 回调与 3D 一致：`OnTriggerEnter2D` / `OnCollisionEnter2D` 等，射线检测使用 `Physics2D.Raycast`
- 参数独立配置：`Edit > Project Settings > Physics 2D`，基础概念（质量、阻力、碰撞矩阵）与 3D 物理对应
- 注意 2D 和 3D 物理对象之间不会发生碰撞 — 它们是两套完全隔离的物理世界

## Ragdoll（布娃娃系统）

给角色骨骼挂载物理组件，使其在失去控制时呈现自然的物理反应：
1. 选中角色根层级，右键 **3D Object > Ragdoll...**（或在 Ragdoll Wizard 中依次指定各骨骼对应的 Transform）
2. Unity 自动为每个指定骨骼创建 CharacterJoint + Rigidbody + CapsuleCollider，关节间互连形成物理链
3. 运行时逻辑：默认由 Animator 驱动；受击/死亡时调用 `animator.enabled = false` 关闭动画，物理接管模拟自然倒地
4. 注意 Ragdoll 性能开销高（多个 Joint 和 Rigidbody），仅在必要时启用，并可设置 **Sleep Threshold** 让静止部分休眠

## DOTS Physics

基于 ECS（Entity Component System）的高性能物理引擎，面向大批量物理对象场景：
- 性能远超传统 GameObject + PhysX 方案，利用 Burst 编译器 + Job System 并行化物理计算
- 核心为 `PhysicsWorld` + `PhysicsCollider` + `PhysicsVelocity` 等 ECS 组件，无 GameObject 开销
- 适用于数千个物理对象同时交互的场景（弹幕射击、大规模破坏效果、物理模拟游戏）
- 与传统 PhysX 不兼容，需在项目初期就选择使用（Unity Physics package 安装即可），与 `Unity.Transforms` / `Unity.Physics` 命名空间配合
