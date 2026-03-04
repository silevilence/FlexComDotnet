namespace FlexComDotnet.Core.Features.Protocol.Models.Dlt645;

/// <summary>
/// DL/T 645-2007 数据标识定义
/// </summary>
public class Dlt645DataIdentifier
{
    /// <summary>
    /// 4字节数据标识 (DI3-DI2-DI1-DI0)
    /// </summary>
    public uint Code { get; init; }

    /// <summary>
    /// 数据项名称
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 数据项描述
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 数据长度 (字节)
    /// </summary>
    public int DataLength { get; init; }

    /// <summary>
    /// 小数位数
    /// </summary>
    public int DecimalPlaces { get; init; }

    /// <summary>
    /// 单位
    /// </summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>
    /// 数据格式类型
    /// </summary>
    public Dlt645DataFormat Format { get; init; } = Dlt645DataFormat.BcdUnsigned;

    /// <summary>
    /// 格式化显示数据标识
    /// </summary>
    public string CodeHex => $"{Code:X8}";
}

/// <summary>
/// DL/T 645-2007 数据格式类型
/// </summary>
public enum Dlt645DataFormat
{
    BcdUnsigned,
    BcdSigned,
    Ascii,
    Binary,
    DateTime,
    Date,
    Time
}

/// <summary>
/// DL/T 645-2007 数据标识字典
/// </summary>
public static class Dlt645DataDictionary
{
    private static readonly Dictionary<uint, Dlt645DataIdentifier> s_identifiers = new()
    {
        // 电能量数据标识 (DI3=00)
        [0x00000000] = new() { Code = 0x00000000, Name = "组合有功总电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },
        [0x00010000] = new() { Code = 0x00010000, Name = "组合有功费率1电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },
        [0x00020000] = new() { Code = 0x00020000, Name = "组合有功费率2电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },
        [0x00030000] = new() { Code = 0x00030000, Name = "组合有功费率3电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },
        [0x00040000] = new() { Code = 0x00040000, Name = "组合有功费率4电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },

        [0x00100000] = new() { Code = 0x00100000, Name = "正向有功总电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },
        [0x00110000] = new() { Code = 0x00110000, Name = "正向有功费率1电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },
        [0x00120000] = new() { Code = 0x00120000, Name = "正向有功费率2电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },
        [0x00130000] = new() { Code = 0x00130000, Name = "正向有功费率3电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },
        [0x00140000] = new() { Code = 0x00140000, Name = "正向有功费率4电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },

        [0x00200000] = new() { Code = 0x00200000, Name = "反向有功总电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },
        [0x00210000] = new() { Code = 0x00210000, Name = "反向有功费率1电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },
        [0x00220000] = new() { Code = 0x00220000, Name = "反向有功费率2电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },
        [0x00230000] = new() { Code = 0x00230000, Name = "反向有功费率3电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },
        [0x00240000] = new() { Code = 0x00240000, Name = "反向有功费率4电能", DataLength = 4, DecimalPlaces = 2, Unit = "kWh" },

        [0x00300000] = new() { Code = 0x00300000, Name = "组合无功1总电能", DataLength = 4, DecimalPlaces = 2, Unit = "kvarh" },
        [0x00400000] = new() { Code = 0x00400000, Name = "组合无功2总电能", DataLength = 4, DecimalPlaces = 2, Unit = "kvarh" },
        [0x00500000] = new() { Code = 0x00500000, Name = "第一象限无功总电能", DataLength = 4, DecimalPlaces = 2, Unit = "kvarh" },
        [0x00600000] = new() { Code = 0x00600000, Name = "第二象限无功总电能", DataLength = 4, DecimalPlaces = 2, Unit = "kvarh" },
        [0x00700000] = new() { Code = 0x00700000, Name = "第三象限无功总电能", DataLength = 4, DecimalPlaces = 2, Unit = "kvarh" },
        [0x00800000] = new() { Code = 0x00800000, Name = "第四象限无功总电能", DataLength = 4, DecimalPlaces = 2, Unit = "kvarh" },

        // 变量数据标识 (DI3=02)
        [0x02010100] = new() { Code = 0x02010100, Name = "A相电压", DataLength = 2, DecimalPlaces = 1, Unit = "V" },
        [0x02010200] = new() { Code = 0x02010200, Name = "B相电压", DataLength = 2, DecimalPlaces = 1, Unit = "V" },
        [0x02010300] = new() { Code = 0x02010300, Name = "C相电压", DataLength = 2, DecimalPlaces = 1, Unit = "V" },

        [0x02020100] = new() { Code = 0x02020100, Name = "A相电流", DataLength = 3, DecimalPlaces = 3, Unit = "A" },
        [0x02020200] = new() { Code = 0x02020200, Name = "B相电流", DataLength = 3, DecimalPlaces = 3, Unit = "A" },
        [0x02020300] = new() { Code = 0x02020300, Name = "C相电流", DataLength = 3, DecimalPlaces = 3, Unit = "A" },

        [0x02030000] = new() { Code = 0x02030000, Name = "瞬时总有功功率", DataLength = 3, DecimalPlaces = 4, Unit = "kW", Format = Dlt645DataFormat.BcdSigned },
        [0x02030100] = new() { Code = 0x02030100, Name = "A相有功功率", DataLength = 3, DecimalPlaces = 4, Unit = "kW", Format = Dlt645DataFormat.BcdSigned },
        [0x02030200] = new() { Code = 0x02030200, Name = "B相有功功率", DataLength = 3, DecimalPlaces = 4, Unit = "kW", Format = Dlt645DataFormat.BcdSigned },
        [0x02030300] = new() { Code = 0x02030300, Name = "C相有功功率", DataLength = 3, DecimalPlaces = 4, Unit = "kW", Format = Dlt645DataFormat.BcdSigned },

        [0x02040000] = new() { Code = 0x02040000, Name = "瞬时总无功功率", DataLength = 3, DecimalPlaces = 4, Unit = "kvar", Format = Dlt645DataFormat.BcdSigned },
        [0x02040100] = new() { Code = 0x02040100, Name = "A相无功功率", DataLength = 3, DecimalPlaces = 4, Unit = "kvar", Format = Dlt645DataFormat.BcdSigned },
        [0x02040200] = new() { Code = 0x02040200, Name = "B相无功功率", DataLength = 3, DecimalPlaces = 4, Unit = "kvar", Format = Dlt645DataFormat.BcdSigned },
        [0x02040300] = new() { Code = 0x02040300, Name = "C相无功功率", DataLength = 3, DecimalPlaces = 4, Unit = "kvar", Format = Dlt645DataFormat.BcdSigned },

        [0x02050000] = new() { Code = 0x02050000, Name = "瞬时总视在功率", DataLength = 3, DecimalPlaces = 4, Unit = "kVA" },
        [0x02060000] = new() { Code = 0x02060000, Name = "总功率因数", DataLength = 2, DecimalPlaces = 3, Unit = "", Format = Dlt645DataFormat.BcdSigned },
        [0x02060100] = new() { Code = 0x02060100, Name = "A相功率因数", DataLength = 2, DecimalPlaces = 3, Unit = "", Format = Dlt645DataFormat.BcdSigned },
        [0x02060200] = new() { Code = 0x02060200, Name = "B相功率因数", DataLength = 2, DecimalPlaces = 3, Unit = "", Format = Dlt645DataFormat.BcdSigned },
        [0x02060300] = new() { Code = 0x02060300, Name = "C相功率因数", DataLength = 2, DecimalPlaces = 3, Unit = "", Format = Dlt645DataFormat.BcdSigned },

        [0x02800002] = new() { Code = 0x02800002, Name = "电网频率", DataLength = 2, DecimalPlaces = 2, Unit = "Hz" },

        // 参变量数据标识 (DI3=04)
        [0x04000101] = new() { Code = 0x04000101, Name = "日期及星期", DataLength = 4, Format = Dlt645DataFormat.DateTime },
        [0x04000102] = new() { Code = 0x04000102, Name = "时间", DataLength = 3, Format = Dlt645DataFormat.Time },
        [0x04000401] = new() { Code = 0x04000401, Name = "通信地址", DataLength = 6, Format = Dlt645DataFormat.Ascii },
        [0x04000402] = new() { Code = 0x04000402, Name = "表号", DataLength = 6, Format = Dlt645DataFormat.Ascii },
        [0x04000404] = new() { Code = 0x04000404, Name = "资产管理编码", DataLength = 32, Format = Dlt645DataFormat.Ascii },
    };

    public static Dlt645DataIdentifier? GetIdentifier(uint code)
    {
        return s_identifiers.GetValueOrDefault(code);
    }

    public static Dlt645DataIdentifier? GetIdentifier(byte di0, byte di1, byte di2, byte di3)
    {
        uint code = (uint)(di3 << 24 | di2 << 16 | di1 << 8 | di0);
        return GetIdentifier(code);
    }

    public static IReadOnlyDictionary<uint, Dlt645DataIdentifier> GetAllIdentifiers() => s_identifiers;

    public static string GetDataName(uint code)
    {
        var identifier = GetIdentifier(code);
        return identifier?.Name ?? $"未知数据项(0x{code:X8})";
    }
}
