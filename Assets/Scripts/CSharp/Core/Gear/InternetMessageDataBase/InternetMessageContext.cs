using System;

/// <summary>
/// 承载一条网络消息的 ID 与消息体长度。
/// </summary>
public sealed class InternetMessageContext
{
    public uint msgID;
    public uint msgLength;
    
    public InternetMessageContext(uint msgID, uint msgLength)
    {
        this.msgID = msgID;
        this.msgLength = msgLength;
    }
}
