public static class MessageTool
{
    public static void Init()
    {
        //此处写网络传输数据的数据类型注册以便解读出网络信息
        InternetMessage.Register((uint)InternetMessageID.PlayerInput, typeof(MsgPlayerInput));
    }
}