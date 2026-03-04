namespace FlexComDotnet.Core.Features.Protocol.Models.Dlt645;

/// <summary>
/// DL/T 645-2007 控制码
/// </summary>
public class Dlt645ControlCode
{
    /// <summary>
    /// 原始控制码字节
    /// </summary>
    public byte RawValue { get; }

    /// <summary>
    /// 功能码 (D0-D4)
    /// </summary>
    public Dlt645FunctionCode FunctionCode { get; }

    /// <summary>
    /// 是否为从站应答 (D7=1)
    /// </summary>
    public bool IsResponse { get; }

    /// <summary>
    /// 是否有后续帧 (D5=1)
    /// </summary>
    public bool HasFollowFrame { get; }

    /// <summary>
    /// 是否为异常应答 (D6=1)
    /// </summary>
    public bool IsError { get; }

    public Dlt645ControlCode(byte value)
    {
        RawValue = value;
        FunctionCode = (Dlt645FunctionCode)(value & 0x1F);
        HasFollowFrame = (value & 0x20) != 0;
        IsError = (value & 0x40) != 0;
        IsResponse = (value & 0x80) != 0;
    }

    public override string ToString()
    {
        var direction = IsResponse ? "从站应答" : "主站请求";
        var status = IsError ? "[异常]" : "";
        return $"{direction}{status} - {GetFunctionDescription()}";
    }

    public string GetFunctionDescription() => FunctionCode switch
    {
        Dlt645FunctionCode.Reserved => "保留",
        Dlt645FunctionCode.ReadData => "读数据",
        Dlt645FunctionCode.ReadFollowData => "读后续数据",
        Dlt645FunctionCode.ReadAddress => "读通信地址",
        Dlt645FunctionCode.WriteData => "写数据",
        Dlt645FunctionCode.WriteAddress => "写通信地址",
        Dlt645FunctionCode.FreezeCommand => "冻结命令",
        Dlt645FunctionCode.ChangeBaudRate => "更改通信速率",
        Dlt645FunctionCode.ChangePassword => "修改密码",
        Dlt645FunctionCode.ClearMaxDemand => "最大需量清零",
        Dlt645FunctionCode.ClearEnergy => "电表清零",
        Dlt645FunctionCode.ClearEvent => "事件清零",
        _ => $"未知功能码(0x{(byte)FunctionCode:X2})"
    };
}

/// <summary>
/// DL/T 645-2007 功能码枚举
/// </summary>
public enum Dlt645FunctionCode : byte
{
    Reserved = 0x00,
    ReadData = 0x11,
    ReadFollowData = 0x12,
    ReadAddress = 0x13,
    WriteData = 0x14,
    WriteAddress = 0x15,
    FreezeCommand = 0x16,
    ChangeBaudRate = 0x17,
    ChangePassword = 0x18,
    ClearMaxDemand = 0x19,
    ClearEnergy = 0x1A,
    ClearEvent = 0x1B
}
