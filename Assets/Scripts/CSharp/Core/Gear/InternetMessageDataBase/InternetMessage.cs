using System;
using System.Buffers.Binary;
using System.Collections.Generic;

/// <summary>
/// 网络消息注册表：为消息类型分配 uint 网络 ID，并提供固定帧头的打包与解析。
/// </summary>
/// <remarks>
/// 数据包格式：前 4 字节为大端消息 ID，随后 4 字节为大端消息体长度，最后为 MessagePack 消息体。
/// 线程安全约束：Register 须在单线程初始化阶段全部完成；此后多线程读取安全。
/// </remarks>
public static class InternetMessage
{
    // 消息 ID 在帧头中占用的字节数。
    public const int MessageIdLength = sizeof(uint);
    // 消息体长度在帧头中占用的字节数。
    public const int PayloadLengthLength = sizeof(uint);
    // 每个网络消息固定帧头的总字节数。
    public const int HeaderLength = MessageIdLength + PayloadLengthLength;

    // 消息 ID 到消息类型的映射。
    private static Dictionary<uint, Type> ID_Type = new();
    // 消息类型到消息 ID 的映射。
    private static Dictionary<Type, uint> Type_ID = new();

    /// <summary>
    /// 注册消息类型与其网络 ID 的双向映射。
    /// </summary>
    /// <param name="id">消息网络 ID，全局唯一。</param>
    /// <param name="type">消息类型，全局唯一。</param>
    /// <exception cref="InvalidOperationException">ID 或类型已被注册时抛出。</exception>
    public static void Register(uint id, Type type)
    {
        // 先完成双侧存在性检查再写入，避免一侧写入成功后另一侧冲突导致半注册状态。
        if (ID_Type.ContainsKey(id)) throw new InvalidOperationException($"ID {id} 已被 {ID_Type[id].FullName} 注册");
        if (Type_ID.ContainsKey(type)) throw new InvalidOperationException($"类型 {type.FullName} 已被 ID {Type_ID[type]} 注册");

        ID_Type.Add(id, type);
        Type_ID.Add(type, id);
    }

    /// <summary>
    /// 判断消息 ID 是否已经注册。
    /// </summary>
    /// <param name="id">待检查的消息网络 ID。</param>
    /// <returns>ID 已注册时返回 true。</returns>
    public static bool IsMessageRegistered(uint id) => ID_Type.ContainsKey(id);

    /// <summary>
    /// 判断消息类型是否已经注册。
    /// </summary>
    /// <param name="type">待检查的消息类型。</param>
    /// <returns>类型已注册时返回 true。</returns>
    public static bool IsMessageRegistered(Type type) => type != null && Type_ID.ContainsKey(type);

    /// <summary>
    /// 按网络 ID 获取已注册的消息类型。
    /// </summary>
    /// <param name="id">消息网络 ID。</param>
    /// <returns>对应的消息类型。</returns>
    /// <exception cref="InvalidOperationException">ID 未注册时抛出。</exception>
    public static Type GetMessageType(uint id)
    {
        if (ID_Type.TryGetValue(id, out Type type)) return type;
        throw new InvalidOperationException($"消息 ID {id} 未注册");
    }

    /// <summary>
    /// 读取数据流中的消息 ID 和消息体长度，返回消息上下文。
    /// </summary>
    /// <param name="bytes">至少包含 8 字节消息帧头的数据流。</param>
    /// <returns>装有消息 ID 和消息体长度的上下文。</returns>
    /// <exception cref="ArgumentNullException">数据流为空时抛出。</exception>
    /// <exception cref="InvalidOperationException">数据流长度不足消息帧头时抛出。</exception>
    public static InternetMessageContext ReadContext(byte[] bytes)
    {
        // 先确认数据流已接收完整帧头。
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < HeaderLength)
            throw new InvalidOperationException($"消息帧长度不足 {HeaderLength} 字节，无法解析帧头。");

        // 从固定帧头读取 ID 与消息体长度，是否收到完整消息体由外部网络层判断。
        ReadOnlySpan<byte> header = bytes.AsSpan(0, HeaderLength);
        uint id = BinaryPrimitives.ReadUInt32BigEndian(header);
        uint payloadLength = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(MessageIdLength));
        return new InternetMessageContext(id, payloadLength);
    }

    /// <summary>
    /// 在消息体前打包大端消息 ID 与消息体长度，生成完整消息帧。
    /// </summary>
    /// <typeparam name="T">消息类型，须已注册。</typeparam>
    /// <param name="bytes">纯消息体字节数组。</param>
    /// <returns>装有本次推入 ID 和消息体长度的上下文。</returns>
    /// <exception cref="ArgumentNullException">消息体为空时抛出。</exception>
    /// <exception cref="InvalidOperationException">消息类型未注册时抛出。</exception>
    public static InternetMessageContext MessageContextPush<T>(ref byte[] bytes)
    {
        // 根据消息类型取得 ID，并根据消息体数组取得长度。
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        if (!Type_ID.TryGetValue(typeof(T), out uint id))
            throw new InvalidOperationException($"{typeof(T).FullName} 未注册，请先调用 {nameof(Register)}");

        // 将固定帧头推入消息体前方，调用后 bytes 可以直接交给底层连接发送。
        uint payloadLength = checked((uint)bytes.Length);
        byte[] frame = new byte[HeaderLength + bytes.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame, id);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(MessageIdLength, PayloadLengthLength), payloadLength);
        bytes.CopyTo(frame, HeaderLength);
        bytes = frame;

        return new InternetMessageContext(id, payloadLength);
    }
}
