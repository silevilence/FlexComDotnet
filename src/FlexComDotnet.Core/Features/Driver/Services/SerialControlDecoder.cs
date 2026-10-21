using System.Buffers.Binary;
using FlexComDotnet.Core.Features.Serial.Helpers;

namespace FlexComDotnet.Core.Features.Driver.Services;

/// <summary>Decodes external serial IOCTLs from ntddser.h; never infers uncaptured bytes.</summary>
internal static class SerialControlDecoder
{
    // Function numbers 1–41, FILE_DEVICE_SERIAL_PORT, METHOD_BUFFERED, FILE_ANY_ACCESS.
    private static readonly (string Name, string Description, bool HasBuffer)[] s_commands =
    [
        ("SET_BAUD_RATE", "设置波特率", true),
        ("SET_QUEUE_SIZE", "设置收发队列大小", true),
        ("SET_LINE_CONTROL", "设置数据位、校验和停止位", true),
        ("SET_BREAK_ON", "开启 BREAK", false),
        ("SET_BREAK_OFF", "关闭 BREAK", false),
        ("IMMEDIATE_CHAR", "立即发送字符", true),
        ("SET_TIMEOUTS", "设置读写超时", true),
        ("GET_TIMEOUTS", "查询读写超时", true),
        ("SET_DTR", "置位 DTR", false),
        ("CLR_DTR", "清除 DTR", false),
        ("RESET_DEVICE", "复位串口设备", false),
        ("SET_RTS", "置位 RTS", false),
        ("CLR_RTS", "清除 RTS", false),
        ("SET_XOFF", "暂停发送（XOFF）", false),
        ("SET_XON", "恢复发送（XON）", false),
        ("GET_WAIT_MASK", "查询等待事件掩码", true),
        ("SET_WAIT_MASK", "设置等待事件掩码", true),
        ("WAIT_ON_MASK", "等待串口事件", true),
        ("PURGE", "清空缓冲区或取消读写", true),
        ("GET_BAUD_RATE", "查询波特率", true),
        ("GET_LINE_CONTROL", "查询数据位、校验和停止位", true),
        ("GET_CHARS", "查询特殊字符", true),
        ("SET_CHARS", "设置特殊字符", true),
        ("GET_HANDFLOW", "查询流控", true),
        ("SET_HANDFLOW", "设置流控", true),
        ("GET_MODEMSTATUS", "查询调制解调器状态", true),
        ("GET_COMMSTATUS", "查询通信状态", true),
        ("XOFF_COUNTER", "设置 XOFF 计数器", true),
        ("GET_PROPERTIES", "查询串口能力", true),
        ("GET_DTRRTS", "查询 DTR/RTS 状态", true),
        ("LSRMST_INSERT", "设置线路/调制解调器状态插入", true),
        ("CONFIG_SIZE", "查询配置大小", true),
        ("GET_COMMCONFIG", "查询串口配置", true),
        ("SET_COMMCONFIG", "设置串口配置", true),
        ("GET_STATS", "查询串口统计", true),
        ("CLEAR_STATS", "清空串口统计", false),
        ("GET_MODEM_CONTROL", "查询调制解调器控制", true),
        ("SET_MODEM_CONTROL", "设置调制解调器控制", true),
        ("SET_FIFO_CONTROL", "设置 FIFO 控制", true),
        ("APPLY_DEFAULT_CONFIGURATION", "应用默认配置", false),
        ("SET_INTERVAL_TIMER_RESOLUTION", "设置间隔计时器分辨率", true)
    ];

    public static (string Name, string Content) Decode(SerialMonitorDataRecord record)
    {
        uint function = (record.Ioctl >> 2) & 0xFFF;
        string hex = HexHelper.BytesToHexString(record.Data);
        // Match the entire code, including access and transfer method, not just function bits.
        if (function < 1 || function > s_commands.Length || record.Ioctl != (0x001B0000u | (function << 2)))
            return ($"IOCTL 0x{record.Ioctl:X8}", $"未知控制指令 0x{record.Ioctl:X8}；原始数据：{(hex.Length == 0 ? "未采集" : hex)}");

        var command = s_commands[function - 1];
        string name = "IOCTL_SERIAL_" + command.Name;
        string description = command.Description;
        if (record.DataUnavailable) return (name, description + "；数据不可用");
        if (!command.HasBuffer) return (name, description);
        if (record.Data.Length == 0) return (name, description + "；未采集参数/返回值");
        if (record.Offset != 0 || (record.Flags & 2) != 0)
            return (name, description + "；数据分片，原始数据：" + hex);

        // The current driver snapshots input for these three SET requests only.
        int required = function switch { 1 => 4, 3 => 3, 25 => 16, _ => 0 };
        if (record.Data.Length < required)
            return (name, $"{description}；参数长度不足（{record.Data.Length}/{required} 字节）");
        ReadOnlySpan<byte> data = record.Data;
        string parameters = function switch
        {
            1 => $"波特率={BinaryPrimitives.ReadUInt32LittleEndian(data)} bps",
            3 => $"数据位={data[2]}；校验={Parity(data[1])}；停止位={StopBits(data[0])}",
            25 => Handflow(data),
            _ => "原始数据：" + hex
        };
        return (name, description + "；" + parameters);
    }

    private static string Parity(byte value) => value switch
    { 0 => "无", 1 => "奇", 2 => "偶", 3 => "标记", 4 => "空格", _ => $"未知({value})" };

    private static string StopBits(byte value) => value switch
    { 0 => "1", 1 => "1.5", 2 => "2", _ => $"未知({value})" };

    private static string Handflow(ReadOnlySpan<byte> data)
    {
        uint handshake = BinaryPrimitives.ReadUInt32LittleEndian(data);
        uint replace = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        string dtr = (handshake & 3) switch { 0 => "关闭", 1 => "开启", 2 => "握手", _ => "未知(3)" };
        string rts = (replace & 0xC0) switch { 0 => "关闭", 0x40 => "开启", 0x80 => "握手", _ => "发送切换" };
        static string OnOff(bool value) => value ? "开" : "关";
        return $"DTR={dtr}；RTS={rts}；CTS握手={OnOff((handshake & 8) != 0)}；" +
            $"DSR握手={OnOff((handshake & 0x10) != 0)}；DCD握手={OnOff((handshake & 0x20) != 0)}；" +
            $"XON/XOFF发送={OnOff((replace & 1) != 0)}；XON/XOFF接收={OnOff((replace & 2) != 0)}；" +
            $"XonLimit={BinaryPrimitives.ReadInt32LittleEndian(data[8..])}；XoffLimit={BinaryPrimitives.ReadInt32LittleEndian(data[12..])}；" +
            $"ControlHandShake=0x{handshake:X8}；FlowReplace=0x{replace:X8}";
    }
}
