---
name: spaced-repetition-quiz
description: 面试八股间隔重复抽查。每次随机切换板块类型，根据历史得分动态调整，绝不连续多题同一板块。
---

# Spaced Repetition Quiz

## 核心规则

### 1. 绝对随机板块切换
- **连续两题绝对不能是同一板块**
- 每道题从不同的模块中轮换抽取
- 全部 17 个模块都可能被抽到（技术原理/引擎系统/算法/图形学/项目/网络/设计模式）

### 2. 根据历史得分调整
- 读取 `quiz-review-tracker.md` 中的得分记录
- **弱项（<70 分）**: 偶尔穿插，不连续轰炸
- **中等（70-84 分）**: 正常频率出现
- **已掌握（>=85 分）**: 基本不出，除非是到期的巩固复习
- **从未答过的板块**: 优先抽，扩大覆盖面

### 3. 题量要求
- **八股文**: 每天 15 题，混合到期弱项 + 旧弱题巩固 + 随机新板块
- **算法题**: 每天 6-9 题（新题 ≥3 + 复习 3-6），独立于八股
- 板块轮换，不连续同一板块

### 4. 题目风格轮换
- 原理题 ↔ 代码题 ↔ 场景设计题 ↔ 对比题 ↔ 实践排错题
- 不要连续问同一类型的题

### 5. 提问后记录
- 得分 < 80 → quiz-review-tracker.md 待复习队列
- 得分 >= 85 → 已掌握
- 记录：日期、题目、得分、核心错误、下次复习日

### 6. 复习间隔
1天 → 3天 → 7天 → 15天 → 通过即标记已掌握

## 模块池（16个）
csharp-fundamentals, cpp-comparison, memory-gc, unity-core, unity-rendering, unity-physics, unity-animation, networking, data-structures-algorithms, design-patterns, performance-optimization, computer-graphics, lua-hotfix, operating-systems, interview-behavioral, project-miniature-garden

## 每日算法题（独立于八股文提问）

- **每轮提问后**，额外抽查 **3 道 LeetCode 算法题**
- 来源：`Obsidian/modules/data-structures-algorithms.md` 中的 Hot 100/150 题库
- 从 必刷120题（数组/链表/栈/哈希表/二叉树）和 分类题型（DP/回溯/贪心/二分/双指针/位运算/堆/图论/前缀和/数学/设计）中**随机交叉抽题**
- 提问方式：只给题号和题目名，例如 "#206 反转链表"
- **用户去 LeetCode 写**，完成后反馈结果（通过/未通过/没做出来）
- 通过 → ⬜ 改 ✅ | 未通过 → 保留分数标记，择机重抽
- **新题与复习分开**：每天新题 3 道 + 复习旧题最多 6 道（总计 ≤9 道）
- 复习题优先抽 🟠（<75分），其次 🟡（75-84分）
- **全部 Hot 100/150 通过后**，解锁游戏场景算法 20 题

## 排除板块
- **databases**: 用户定位小白，暂不提问
