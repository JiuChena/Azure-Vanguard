---
tags: [networking, multiplayer, unity]
created: 2026-06-20
---

# 网络编程与多人游戏

## 1. TCP 与 UDP 对比

### TCP（传输控制协议）

- **面向连接**：通信前需要三次握手建立连接，通信结束后四次挥手断开
- **可靠传输**：保证数据按序到达、不丢失、不重复，通过确认应答和重传机制实现
- **流量控制与拥塞控制**：滑动窗口机制，防止发送方过快导致接收方缓冲区溢出
- **适用场景**：对可靠性要求高的场景，如 HTTP 请求、文件传输、聊天消息、登录认证
- **性能特点**：延迟较高，带宽开销大（头部 20 字节），因为需要 ACK 确认

### UDP（用户数据报协议）

- **无连接**：直接发送数据报，无需建立连接
- **不可靠传输**：不保证送达、不保证顺序、不保证不重复
- **无流量控制**：应用层需要自行处理丢包和乱序
- **适用场景**：实时性要求高的场景，如 FPS 游戏的状态同步、VoIP、视频直播
- **性能特点**：低延迟，头部仅 8 字节，适合高频小数据包传输

### 游戏中 TCP vs UDP 的选择

| 场景 | 推荐协议 | 理由 |
|------|----------|------|
| 登录、匹配、商城 | TCP | 需要可靠传输 |
| FPS 射击同步 | UDP | 低延迟，丢一帧可容忍 |
| MOBA 帧同步 | UDP | 低延迟，可自定义可靠层 |
| MMO 状态同步 | TCP/UDP 混合 | 关键数据 TCP，位置等高频数据 UDP |

### TCP 三次握手与四次挥手

```
三次握手：
Client → Server: SYN
Server → Client: SYN + ACK
Client → Server: ACK

四次挥手：
Client → Server: FIN
Server → Client: ACK
Server → Client: FIN
Client → Server: ACK
```

> ⚠️ OCR 原文多处提及 TCP 三次握手与 socket 编程相关话题，但由于图像质量限制，具体答题细节无法完整还原。以上为根据面试常见问答补充的标准知识点。

---

## 2. Socket 编程

### 基本概念

Socket 是应用层与传输层之间的抽象接口，提供网络通信的端点。常见模型：

- **阻塞 I/O**：调用 `recv()` 时线程阻塞直到收到数据
- **非阻塞 I/O**：`recv()` 立即返回，没有数据时返回错误
- **I/O 多路复用**：`select` / `poll` / `epoll`（Linux）或 IOCP（Windows），一个线程管理多个 socket

### C# 中的 Socket 示例

```csharp
// TCP Server
using System.Net;
using System.Net.Sockets;

public class TcpServer
{
    private TcpListener listener;
    
    public void Start(int port)
    {
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        listener.BeginAcceptTcpClient(OnClientConnected, null);
    }
    
    private void OnClientConnected(IAsyncResult ar)
    {
        TcpClient client = listener.EndAcceptTcpClient(ar);
        // 处理客户端连接...
        listener.BeginAcceptTcpClient(OnClientConnected, null);
    }
}

// UDP Socket
public class UdpSocket
{
    private UdpClient udpClient;
    
    public void Start(int port)
    {
        udpClient = new UdpClient(port);
        udpClient.BeginReceive(OnReceive, null);
    }
    
    private void OnReceive(IAsyncResult ar)
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        byte[] data = udpClient.EndReceive(ar, ref remoteEP);
        // 处理数据...
        udpClient.BeginReceive(OnReceive, null);
    }
}
```

> ⚠️ OCR 原文提及 socket 编程相关问题较多，但具体答题示例不够完整。以上代码为根据面试考点补充的标准 C# Socket 实现。

---

## 3. HTTP 与 HTTPS

### HTTP 协议

- **HTTP/1.0**：每次请求建立一次 TCP 连接，请求完毕后断开（短连接）
- **HTTP/1.1**：默认支持持久连接（Keep-Alive），复用 TCP 连接，支持管线化
- **HTTP/2**：二进制分帧、多路复用、头部压缩、服务器推送
- **HTTP/3**：基于 QUIC（UDP），减少连接建立延迟

### HTTPS 原理

```
1. Client Hello → 客户端支持的加密算法列表
2. Server Hello → 服务端选择算法 + 证书（含公钥）
3. 客户端验证证书有效性
4. 客户端生成对称密钥，用服务端公钥加密后发送
5. 后续通信使用对称密钥加密（AES 等）
```

- **SSL/TLS**：安全传输层协议，HTTPS = HTTP + SSL/TLS
- **证书链**：根证书 → 中间证书 → 服务器证书
- **核心保障**：加密（防窃听）、完整性（防篡改）、身份认证（防冒充）

### HTTP 与 WebSocket 的区别

| 特性 | HTTP | WebSocket |
|------|------|-----------|
| 通信方式 | 请求-响应 | 全双工 |
| 连接 | 短连接/长连接 | 持久连接（一次握手后持续通信） |
| 服务端推送 | 需轮询或 SSE | 原生支持 |
| 适用场景 | REST API、资源获取 | 实时消息推送、游戏聊天 |

---

## 4. Protobuf 序列化

### 为什么选择 Protobuf

Protobuf（Protocol Buffers）是 Google 开发的二进制序列化格式，相比 JSON：

- **体积更小**：相比 JSON 可显著减少数据量
- **解析更快**：二进制格式，无需解析文本
- **强类型**：通过 `.proto` 文件定义数据结构，自动生成代码
- **向后兼容**：字段通过编号标识，支持安全地添加/删除字段

### C# 中使用 protobuf-net

```csharp
[ProtoContract]
public class PlayerData
{
    [ProtoMember(1)]
    public int PlayerId { get; set; }
    
    [ProtoMember(2)]
    public string Name { get; set; }
    
    [ProtoMember(3)]
    public float PositionX { get; set; }
    
    [ProtoMember(4)]
    public float PositionY { get; set; }
    
    [ProtoMember(5)]
    public float PositionZ { get; set; }
}

// 序列化
using (var stream = new MemoryStream())
{
    Serializer.Serialize(stream, playerData);
    byte[] data = stream.ToArray();
}

// 反序列化
using (var stream = new MemoryStream(data))
{
    PlayerData player = Serializer.Deserialize<PlayerData>(stream);
}
```

---

## 5. 帧同步 vs 状态同步

### 帧同步（Lockstep / Frame Synchronization）

- **原理**：所有客户端执行相同的逻辑帧，服务端只转发操作指令
- **确定性**：要求所有客户端的逻辑计算完全一致（浮点数确定性需特殊处理）
- **应用**：RTS（星际争霸）、MOBA（王者荣耀）、格斗游戏

```
帧同步流程：
1. 客户端收集本局玩家的操作输入
2. 服务端收集所有玩家操作，按帧打包广播
3. 客户端收到后在本机执行相同逻辑
4. 保证同一帧所有客户端结果一致
```

- **优点**：带宽占用小（只传操作），可支持大规模单位
- **缺点**：对网络延迟敏感（需等齐所有玩家操作），确定性难以保证

### 状态同步（State Synchronization）

- **原理**：服务端是权威端，客户端只做表现，服务端同步对象状态
- **应用**：FPS（CS:GO）、MMO（魔兽世界）

```
状态同步流程：
1. 客户端发送操作到服务端
2. 服务端计算游戏逻辑，更新对象状态
3. 服务端将状态变更同步给所有相关客户端
4. 客户端根据收到状态做插值/预测
```

- **优点**：防作弊能力强，客户端无需完整逻辑
- **缺点**：带宽占用较大，服务端计算压力大

### Unity 中的网络同步方案

| 方案 | 类型 | 适用场景 |
|------|------|----------|
| Unity Netcode (NGO) | 状态同步 | 中小型多人游戏 |
| Photon Unity Networking | 状态同步 | 各类多人游戏（云服务） |
| Mirror | 状态同步 | 社区方案，Uno 风格 |
| 自研帧同步框架 | 帧同步 | MOBA/RTS 类项目 |

---

## 6. 客户端-服务器架构

### 基本架构模式

```
┌─────────┐     ┌─────────────┐     ┌─────────┐
│ Client A │◄───►│   Server    │◄───►│ Client B │
└─────────┘     │  (权威端)   │     └─────────┘
                └─────────────┘
```

- **权威服务器（Authoritative Server）**：所有游戏逻辑在服务端执行，客户端只负责输入和渲染
- **P2P 模式**：玩家之间直连，适用于小规模联机
- **C/S + P2P 混合**：核心逻辑走服务器，语音/聊天走 P2P

### 关键模块

- **网络层**：TCP/UDP 通信管理
- **序列化层**：数据打包/解包
- **同步层**：对象状态同步、RPC 调用
- **插值与预测**：客户端侧补偿网络延迟（航位推测法、插值回滚）

---

## 7. 网络传输数据压缩

### 常见优化策略

1. **使用更小的数据类型**：能用 `byte`/`short` 就不用 `int`，能用 `int` 就不用 `long`
2. **浮点数压缩**：将 `float` 转为 `short`（精度降为 1/65535 范围）
3. **增量编码**：只发送变化量而非绝对值
4. **位压缩**：将多个布尔标志打包到单个字节
5. **字符串优化**：使用枚举/ID 代替字符串

### 浮点数转 Short 压缩

```csharp
// float (4 bytes) → short (2 bytes)，压缩率 50%
public static short CompressFloat(float value, float min, float max)
{
    float normalized = (value - min) / (max - min);
    return (short)(normalized * ushort.MaxValue);
}

public static float DecompressFloat(short compressed, float min, float max)
{
    float normalized = (float)compressed / ushort.MaxValue;
    return normalized * (max - min) + min;
}

// 示例：位置压缩（范围 0-1000）
short compressedX = CompressFloat(playerPos.x, 0f, 1000f);
float decompressedX = DecompressFloat(compressedX, 0f, 1000f);
```

### CompactTransform 模式

```csharp
// 将 Transform 数据压缩为紧凑字节数组
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CompactTransform
{
    public uint objectId;         // 4 bytes
    public short posX;            // 2 bytes (压缩 float)
    public short posY;            // 2 bytes
    public short posZ;            // 2 bytes
    public byte rotY;             // 1 byte (Y轴旋转，256档)
    // Total: 11 bytes vs 28 bytes (float*7)
}

// ⚠️ 注意：上例为示意，实际使用需根据项目精度需求调整范围
```

---

## 8. 自定义序列化

### 为什么需要自定义序列化

- 通用序列化框架（如 BinaryFormatter）会产生额外元数据开销
- 可以精确控制每个字段的字节布局
- 极致的带宽优化

### 手动序列化示例

```csharp
public class CustomSerializer
{
    // 写入
    public static void WritePlayerState(byte[] buffer, ref int offset, PlayerState state)
    {
        BitConverter.GetBytes(state.playerId).CopyTo(buffer, offset); offset += 4;
        BitConverter.GetBytes(CompressFloat(state.x, -1000f, 1000f)).CopyTo(buffer, offset); offset += 2;
        BitConverter.GetBytes(CompressFloat(state.y, -1000f, 1000f)).CopyTo(buffer, offset); offset += 2;
        BitConverter.GetBytes(CompressFloat(state.z, -1000f, 1000f)).CopyTo(buffer, offset); offset += 2;
        buffer[offset++] = (byte)(state.hp);
        buffer[offset++] = (byte)(state.flags);
    }
    
    // 读取
    public static PlayerState ReadPlayerState(byte[] buffer, ref int offset)
    {
        PlayerState state = new PlayerState();
        state.playerId = BitConverter.ToInt32(buffer, offset); offset += 4;
        state.x = DecompressFloat(BitConverter.ToInt16(buffer, offset), -1000f, 1000f); offset += 2;
        state.y = DecompressFloat(BitConverter.ToInt16(buffer, offset), -1000f, 1000f); offset += 2;
        state.z = DecompressFloat(BitConverter.ToInt16(buffer, offset), -1000f, 1000f); offset += 2;
        state.hp = buffer[offset++];
        state.flags = buffer[offset++];
        return state;
    }
}
```

---

## 9. NetworkManager 模式

### Unity 中的网络管理器

Unity Netcode for GameObjects (NGO) 提供 `NetworkManager` 组件：

```csharp
using Unity.Netcode;
using UnityEngine;

public class GameNetworkManager : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 服务端逻辑
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
        
        if (IsClient)
        {
            // 客户端逻辑
        }
    }
    
    // Host / Server / Client 启动
    public void StartHost() => NetworkManager.Singleton.StartHost();
    public void StartServer() => NetworkManager.Singleton.StartServer();
    public void StartClient() => NetworkManager.Singleton.StartClient();
    
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected");
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected");
    }
}
```

### NetworkVariable 同步变量

```csharp
public class PlayerNetwork : NetworkBehaviour
{
    // 服务端修改，自动同步到所有客户端
    public NetworkVariable<int> Health = new NetworkVariable<int>(100);
    public NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();
    
    public override void OnNetworkSpawn()
    {
        Health.OnValueChanged += OnHealthChanged;
    }
    
    private void OnHealthChanged(int oldValue, int newValue)
    {
        Debug.Log($"HP changed: {oldValue} → {newValue}");
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damage)
    {
        Health.Value -= damage;
        if (Health.Value <= 0)
        {
            // 处理死亡逻辑
        }
    }
}
```

### 网络管理器设计要点

- **连接管理**：处理连接/断开/重连、超时检测
- **房间管理**：创建/加入/离开房间
- **对象管理**：NetworkObject 生成与销毁、所有权管理
- **网络拓扑**：Host 模式（Client + Server 合一）vs Dedicated Server（专用服务器）
- **NAT 穿透**：处理内网穿透问题（通过中继服务器 Relay）

---

## 面试常见问题总结

根据 OCR 材料中面试记录的归纳：

1. **TCP 和 UDP 的区别是什么？**
2. **什么时候用 TCP，什么时候用 UDP？（如：回合制用 TCP，FPS 用 UDP）**
3. **TCP 三次握手和四次挥手的过程**
4. **HTTP 和 HTTPS 的区别，HTTPS 加密过程**
5. **HTTP/1.0 与 HTTP/1.1 的区别**
6. **HTTP 与 WebSocket 的区别及各自应用场景**
7. **Socket 编程的基本流程**
8. **如何设计网络数据传输格式以提高效率**
9. **Protobuf 与 JSON 的对比**
10. **帧同步与状态同步的优缺点**
11. **Unity 中的网络同步方案有哪些**

> ⚠️ OCR 材料中部分条目仅有题目关键词，缺少完整答案内容。以上笔记结合了 OCR 提取的关键词与标准化知识补充。
