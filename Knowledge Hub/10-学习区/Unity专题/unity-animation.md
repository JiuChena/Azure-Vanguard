---
tags:
  - unity
  - animation
created: 2026-06-20
---

# Unity 动画系统

## Animation vs Animator

Unity 提供两套动画系统：

### Animation（旧版 Legacy）

- 旧版组件，直接挂载 Animation Clip 播放
- 通过代码调用 `animation.Play("ClipName")` 控制
- 不支持状态机，适合简单的单一动画播放
- 在 Rig 面板中将 Animation Type 设为 **Legacy** 才可使用

```csharp
public class LegacyAnimationDemo : MonoBehaviour
{
    [SerializeField] private Animation anim;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            anim.Play("Idle");
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            anim.Play("Run");
    }
}
```

### Animator（新版 Mecanim）

- 基于 **Animator Controller** 状态机的动画系统
- 支持动画混合、分层、IK、状态转换
- 当前版本的主流方案，推荐所有新项目使用

**最佳实践**: 新项目一律使用 **Animator + Animator Controller**，旧项目可渐进迁移。

---

## Animator Controller（动画控制器）

Animator Controller 是 Mecanim 的核心，使用状态机管理动画状态和转换。

### 参数类型

| 参数类型 | 说明 | 用法 |
|----------|------|------|
| **Float** | 浮点参数 | 速度、转向量等连续值 |
| **Int** | 整数参数 | 武器类型、状态索引 |
| **Bool** | 布尔参数 | 是否在地面、是否瞄准 |
| **Trigger** | 触发器（单次） | 跳跃、攻击、受伤等瞬时动作 |

### 代码控制

```csharp
Animator anim = GetComponent<Animator>();

// 设置连续参数
float h = Input.GetAxis("Horizontal");
anim.SetFloat("Speed", Mathf.Abs(h));

// 设置触发器
if (Input.GetKeyDown(KeyCode.Space))
    anim.SetTrigger("Jump");

// 直接播放指定状态
anim.Play("Run");
```

---

## Animation Clips

Animation Clip 是包含关键帧数据的动画片段，定义了物体属性随时间的变化。

### 关键特性

- **关键帧动画**: 在时间轴上设置关键帧，Unity 自动插值
- **动画曲线 (Animation Curves)**: 精确控制属性变化速度
- **动画事件 (Animation Events)**: 在特定帧触发函数调用（如脚步声、攻击判定）

### 动画事件示例

```csharp
// 在特定帧调用此函数（通过 Animation 窗口添加 Event）
public void OnFootstep()
{
    AudioSource.PlayClipAtPoint(footstepSound, transform.position);
}

public void OnAttackHit()
{
    // 攻击判定逻辑
}
```

---

## 状态转换 (State Transitions)

Animator 状态之间的切换通过 **Transition**（过渡线）连接，支持以下特性：

- **Conditions**: 基于参数的转换条件（如 Speed > 0.1）
- **Has Exit Time**: 是否等待当前动画播放到一定比例再切换
- **Transition Duration / Offset**: 过渡时长和偏移
- **Interruption Source**: 动画被打断的策略

### 典型转换配置

```
Idle → Walk:   Speed > 0.1
Walk → Run:    Speed > 0.5 (且按下 Shift)
Run → Walk:    Speed < 0.5
Any State → Death:  Trigger "Dead"
```

---

## Blend Tree（混合树）

Blend Tree 允许根据参数值在多个动画之间平滑混合。

### 1D Blend Tree

根据单个参数（如速度）混合多个动画：

```csharp
// 根据 Speed 参数混合 Idle → Walk → Run
anim.SetFloat("Speed", moveSpeed); // Speed: 0(Idle) → 0.5(Walk) → 1(Run)
```

### 2D Blend Tree

根据两个参数（如水平和垂直输入）混合方向动画：

```csharp
anim.SetFloat("Horizontal", Input.GetAxis("Horizontal"));
anim.SetFloat("Vertical", Input.GetAxis("Vertical"));
```

### 适用场景

- 角色八方向移动
- 射击游戏中瞄准方向的动画混合
- 载具转向与速度的混合

---

## IK (Inverse Kinematics)

反向动力学，根据末端位置（手、脚）自动计算关节旋转。

```csharp
void OnAnimatorIK(int layerIndex)
{
    // 头部注视
    anim.SetLookAtWeight(1f);
    anim.SetLookAtPosition(target.position);

    // 手部 IK（抓取物体）
    anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
    anim.SetIKPosition(AvatarIKGoal.RightHand, target.position);
    
    anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 1f);
    anim.SetIKRotation(AvatarIKGoal.RightHand, target.rotation);
}
```

### Root Motion

- 动画驱动角色位移（而非代码控制移动距离）
- 在 Animator 中勾选 **Apply Root Motion**
- 适合动作游戏的攻击位移、翻滚等精确位移

---

## Avatar Mask

通过遮罩控制动画只应用于身体的特定部位。

**典型应用**: 
- 上半身播放射击/换弹动画，下半身播放跑步动画（分层动画）
- 头部播放注视动画（Look At）

通过 **Animator Layers** 配合 Avatar Mask 实现分层动画。

---

## Timeline

Timeline 是 Unity 的动画编排工具，用于创建过场动画和动画序列。

### 功能特性

- 多轨道编排（Animation Track、Audio Track、Activation Track、Signal Track）
- 与 **Cinemachine** 结合实现电影级过场
- 支持自定义轨道扩展

```csharp
PlayableDirector director = GetComponent<PlayableDirector>();
director.Play();         // 播放
director.Pause();        // 暂停
director.Stop();         // 停止

// Timeline 完成时回调
director.stopped += OnTimelineFinished;
```

---

## Spine 动画集成

Spine 是 2D 骨骼动画的主流方案。

### 优势

- 骨骼动画，比逐帧动画节省大量资源
- 支持动画混合、程序化控制
- 原生支持换装系统
- 运行时文件体积小

### Unity 中集成

```csharp
SkeletonAnimation skeleton = GetComponent<SkeletonAnimation>();

// 播放动画
skeleton.AnimationState.SetAnimation(0, "walk", true);

// 动画混合
skeleton.AnimationState.SetAnimation(0, "run", true);
skeleton.AnimationState.AddAnimation(1, "shoot", false, 0.2f);

// 翻转角色
skeleton.skeleton.ScaleX = facingRight ? 1 : -1;

// 换装
skeleton.skeleton.SetSkin("Armor_01");
```

---

## 动画系统最佳实践

- 使用 **AnimatorOverrideController** 实现动画复用（不同角色共用逻辑，替换动画片段）
- 合理设置 **Culling Mode**（Always Animate / Cull Update Transforms / Cull Completely）
- 避免在 Update 中频繁修改 Animator 参数，使用有意义的参数变化
- Timeline 用于过场动画而非实时游戏逻辑
- 动画事件中避免重量级操作
- Animator Controller 使用子状态机 (Sub-State Machine) 组织复杂逻辑

---

## Playable API

代码驱动的动画控制方案，比 Animator Controller 更灵活，适合程序化动画混合：
- 核心结构为 **Graph → Output → Playable** 节点链，所有节点以树形组织在 `PlayableGraph` 中
- 通过 `AnimationMixerPlayable` 手动控制多个动画的权重混合，可动态插入/移除动画片段
- 与 Animator Controller 可以共存（通过 `AnimatorControllerPlayable` 桥接），但在 Playable API 模式下可完全绕过状态机
- 适合需要精确帧控制和动态动画组合的场景（如格斗游戏连招系统、交互式对话表情）

```csharp
PlayableGraph graph = PlayableGraph.Create("MyGraph");
AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Output", animator);
var mixer = AnimationMixerPlayable.Create(graph, 2);
output.SetSourcePlayable(mixer);
// 创建 ClipPlayable 并连接到 Mixer...
graph.Play();
```

## Animation Rigging

Unity 官方便利包，对骨骼进行程序化控制，适合角色与场景交互：
- **Two Bone IK**: 最常用，控制手臂或腿的两段骨骼 IK 解算（如手抓取物体、脚适配地形）
- **Look At**: 控制头部/眼睛骨骼注视目标点
- **IK/CCD IK**: 多关节链式 IK（尾巴、触手等）
- 使用 **Rig Builder** 组件挂载 Rig 配置，运行时通过权重动态开启/关闭约束效果

## 动画优化

- **压缩 (Keyframe Reduction)**: 移除误差在阈值内的冗余关键帧，减少动画文件体积。在 Animation Clip 导入设置中调整 `Rotation Error` / `Position Error` / `Scale Error`
- **减少骨骼数**: 骨骼数量直接影响 GPU Skinning 开销。约简不必要的骨骼层级，合并小骨骼
- **Culling Mode**: Animator 组件中的 `Culling Mode` 设置 — **Cull Update Transforms**（只更新根节点位置，子骨骼跳过更新）或 **Cull Completely**（屏幕外完全停止动画更新），可大幅减少 CPU 开销

## 程序化动画

运行时通过代码动态生成/调整动画，替代或增强预制动画片段：
- **脚部 IK 适配地形**: 用 Raycast 检测地面高度，通过 Two Bone IK 调整脚部位置，使角色站斜坡时双脚贴合地面而不是悬空或穿插
- **LookAt 跟随目标**: 用 `OnAnimatorIK` 或 Animation Rigging LookAt 控制头部/上半身朝向动态目标
- **布娃娃受击**: 给角色骨骼挂载 Rigidbody + CharacterJoint，在受击瞬间关闭 Animator 让物理接管，模拟自然倒地死亡效果
