using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ComponentModel;
using Microsoft.Win32.SafeHandles;

namespace FlexComDotnet.Core.Features.Driver.Services;

public enum SerialMonitorDataDirection : byte
{
    Tx = 0,
    Rx = 1,
    Control = 2
}

public enum SerialMonitorState : byte
{
    Stopped = 0,
    Running = 1
}

public sealed record SerialMonitorDataRecord(DateTime Timestamp, SerialMonitorDataDirection Direction, byte[] Data)
{
    public bool IsTx => Direction == SerialMonitorDataDirection.Tx;
    public ulong Sequence { get; init; }
    public ulong RequestId { get; init; }
    public ulong PortId { get; init; }
    public int Status { get; init; }
    public uint Ioctl { get; init; }
    public uint RequestedLength { get; init; }
    public uint TransferredLength { get; init; }
    public uint Offset { get; init; }
    public byte Kind { get; init; }
    public ushort Flags { get; init; }
    public bool IsFrame { get; init; }
    public bool DataUnavailable => (Flags & 1) != 0;
}

public sealed record SerialMonitorStatus(
    SerialMonitorState State,
    uint CapturedEntryCount,
    ulong TotalBytesRx,
    ulong TotalBytesTx,
    ulong DroppedRecords = 0,
    ulong DroppedBytes = 0,
    ulong PortId = 0);

public sealed record SerialMonitorDiagnostics(
    uint LastStartStage,
    int LastStartStatus,
    uint PnpFilterCount,
    uint FilterCreateCount,
    int LastFilterCreateStatus)
{
    public string ToDisplayText() =>
        $"启动阶段={GetStageName(LastStartStage)}({LastStartStage}) NTSTATUS=0x{unchecked((uint)LastStartStatus):X8}; " +
        $"PnP过滤设备={PnpFilterCount}；外部打开请求={FilterCreateCount}，最后状态=0x{unchecked((uint)LastFilterCreateStatus):X8}";

    private static string GetStageName(uint stage) => stage switch
    {
        1 => "创建过滤设备",
        2 => "附加目标串口",
        3 => "配置过滤设备",
        4 => "启用过滤设备",
        5 => "监控运行中",
        _ => "未启动"
    };
}

public sealed record SerialMonitorAvailability(
    bool IsAdministrator,
    bool IsDriverAvailable,
    bool CanMonitor,
    string Message);

/// <summary>
/// C# 用户态 SerialMonitor 驱动访问层。
/// </summary>
public sealed class SerialMonitorService : IDisposable
{
    private readonly DriverLifecycleService _driverLifecycleService;
    private readonly Func<ISerialMonitorDevice> _createDevice;
    private readonly Func<bool> _isAdministrator;
    private readonly Func<string, string> _resolvePortDevicePath;
    private readonly bool _startReadLoopOnStart;
    private ISerialMonitorDevice? _device;
    private CancellationTokenSource? _readLoopCts;
    private Task? _readLoopTask;
    private bool _disposed;

    public SerialMonitorService()
        : this(
            new DriverLifecycleService(),
            () => new Win32SerialMonitorDevice(),
            IsCurrentProcessAdministrator,
            resolvePortDevicePath: ResolvePortDevicePath)
    {
    }

    internal SerialMonitorService(
        DriverLifecycleService driverLifecycleService,
        Func<ISerialMonitorDevice> createDevice,
        Func<bool> isAdministrator,
        bool startReadLoopOnStart = true,
        Func<string, string>? resolvePortDevicePath = null)
    {
        _driverLifecycleService = driverLifecycleService;
        _createDevice = createDevice;
        _isAdministrator = isAdministrator;
        _startReadLoopOnStart = startReadLoopOnStart;
        _resolvePortDevicePath = resolvePortDevicePath ?? ResolvePortDevicePath;
    }

    public bool IsAdministrator => _isAdministrator();

    public bool IsMonitoring { get; private set; }

    public event EventHandler<SerialMonitorDataRecord>? DataReceived;

    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<SerialMonitorStatus>? StatusChanged;

    public SerialMonitorAvailability CheckAvailability()
    {
        if (!IsAdministrator)
        {
            return new(false, false, false, "需管理员权限");
        }

        var lifecycle = _driverLifecycleService.EnsureInstalledAndStarted();
        if (!lifecycle.Succeeded)
        {
            return new(true, false, false, lifecycle.Message);
        }

        using var device = _createDevice();
        if (!device.Open()) return new(true, false, false, "SerialMonitor 驱动不可用。");
        var output = new byte[SerialMonitorInterop.SizeOf<SerialMonitorInterop.DriverStatus>()];
        var compatible = device.DeviceIoControl(SerialMonitorInterop.IoctlGetStatus, null, output, out var count)
            && count == output.Length && SerialMonitorInterop.Deserialize<SerialMonitorInterop.DriverStatus>(output).Version == 2;
        return new(true, compatible, compatible, compatible
            ? "驱动可用；首次使用需登记目标端口并在业务停止后重新枚举设备。"
            : "驱动版本不兼容，请部署 v2 驱动并重新加载后监控。");
    }

    public DriverLifecycleResult StartMonitoring(string portName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsMonitoring) return DriverLifecycleResult.Failure("请先停止当前监控会话。");
        if (string.IsNullOrWhiteSpace(portName))
        {
            return DriverLifecycleResult.Failure("监控串口名称不能为空。");
        }

        if (portName.Length >= SerialMonitorInterop.MaxPortNameLength)
        {
            return DriverLifecycleResult.Failure($"监控串口名称不能超过 {SerialMonitorInterop.MaxPortNameLength - 1} 个字符。");
        }

        var ready = EnsureReady();
        if (!ready.Succeeded)
        {
            return ready;
        }

        string devicePath;
        try
        {
            devicePath = _resolvePortDevicePath(portName);
        }
        catch (Win32Exception ex)
        {
            return DriverLifecycleResult.Failure($"无法解析串口 {portName} 对应的内核设备：{ex.Message}");
        }

        if (devicePath.Length >= SerialMonitorInterop.MaxPortNameLength)
            return DriverLifecycleResult.Failure("目标设备标识过长，无法启动监控。");

        var request = SerialMonitorInterop.StartMonitorRequest.FromPortName(devicePath);
        if (!_device!.DeviceIoControl(
                SerialMonitorInterop.IoctlStartMonitor,
                SerialMonitorInterop.Serialize(request),
                [],
                out _))
        {
            var diagnostic = TryGetDiagnostics();
            var detail = diagnostic is null
                ? $"Win32Error={_device.LastError} (0x{_device.LastError:X8})"
                : $"Win32Error={_device.LastError} (0x{_device.LastError:X8})；{diagnostic.ToDisplayText()}";
            return DriverLifecycleResult.Failure($"启动串口监控失败：{detail}。请确认目标端口已登记并重新枚举，且使用 v2 驱动。");
        }

        IsMonitoring = true;
        if (_startReadLoopOnStart)
        {
            StartReadLoop();
        }

        return DriverLifecycleResult.Success("串口监控已启动。");
    }

    /// <summary>Registers the selected port for the next device enumeration; never restarts a live port.</summary>
    public DriverLifecycleResult ApplyPnpFilter(string portName)
    {
        if (IsMonitoring) return DriverLifecycleResult.Failure("请先停止监控，再登记端口驱动。");
        var ready = _driverLifecycleService.EnsureInstalledAndStarted();
        return ready.Succeeded ? _driverLifecycleService.ReenumeratePort(portName) : ready;
    }

    public DriverLifecycleResult StopMonitoring()
    {
        StopReadLoop();

        if (_device is not { IsOpen: true })
        {
            IsMonitoring = false;
            return DriverLifecycleResult.Success("串口监控已停止。");
        }

        var ok = _device.DeviceIoControl(SerialMonitorInterop.IoctlStopMonitor, null, [], out _);
        _device.Dispose();
        _device = null;
        IsMonitoring = false;

        return ok
            ? DriverLifecycleResult.Success("串口监控已停止。")
            : DriverLifecycleResult.Failure("停止串口监控失败。");
    }

    public SerialMonitorStatus? GetStatus()
    {
        if (_device is not { IsOpen: true } && !EnsureReady().Succeeded)
        {
            return null;
        }

        return ReadStatus(_device);
    }

    private static SerialMonitorStatus? ReadStatus(ISerialMonitorDevice? device)
    {
        if (device is not { IsOpen: true }) return null;
        var output = new byte[SerialMonitorInterop.SizeOf<SerialMonitorInterop.DriverStatus>()];
        if (!device.DeviceIoControl(SerialMonitorInterop.IoctlGetStatus, null, output, out var bytesReturned)
            || bytesReturned < output.Length)
        {
            return null;
        }

        var status = SerialMonitorInterop.Deserialize<SerialMonitorInterop.DriverStatus>(output);
        if (status.Version != 2) return null;
        return new SerialMonitorStatus(
            (SerialMonitorState)status.State,
            status.CapturedEntryCount,
            status.TotalBytesRx,
            status.TotalBytesTx, status.DroppedRecords, status.DroppedBytes, status.PortId);
    }

    /// <summary>读取驱动保存的启动和外部打开请求诊断信息。</summary>
    public SerialMonitorDiagnostics? GetDiagnostics()
    {
        var ready = EnsureReady();
        return ready.Succeeded ? TryGetDiagnostics() : null;
    }

    private SerialMonitorDiagnostics? TryGetDiagnostics()
    {
        var device = _device;
        if (device is not { IsOpen: true })
        {
            return null;
        }

        var output = new byte[SerialMonitorInterop.SizeOf<SerialMonitorInterop.DriverDiagnostics>()];
        if (!device.DeviceIoControl(SerialMonitorInterop.IoctlGetDiagnostics, null, output, out var bytesReturned)
            || bytesReturned < output.Length)
        {
            return null;
        }

        var diagnostic = SerialMonitorInterop.Deserialize<SerialMonitorInterop.DriverDiagnostics>(output);
        return new SerialMonitorDiagnostics(
            diagnostic.LastStartStage,
            diagnostic.LastStartStatus,
            diagnostic.PnpFilterCount,
            diagnostic.FilterCreateCount,
            diagnostic.LastFilterCreateStatus);
    }

    public bool TryReadData(out SerialMonitorDataRecord? record)
        => TryReadDataFrom(_device, out record);

    private static bool TryReadDataFrom(ISerialMonitorDevice? device, out SerialMonitorDataRecord? record)
    {
        record = null;

        if (device is not { IsOpen: true })
        {
            return false;
        }

        var output = new byte[SerialMonitorInterop.SizeOf<SerialMonitorInterop.GetDataResponse>()];
        if (!device.DeviceIoControl(SerialMonitorInterop.IoctlGetData, null, output, out var bytesReturned)
            || bytesReturned < SerialMonitorInterop.SizeOf<SerialMonitorInterop.CapturedDataHeader>())
        {
            return false;
        }

        var response = SerialMonitorInterop.Deserialize<SerialMonitorInterop.GetDataResponse>(output);
        if (response.Header.DataLength > SerialMonitorInterop.MaxDataSize
            || bytesReturned < SerialMonitorInterop.SizeOf<SerialMonitorInterop.CapturedDataHeader>() + response.Header.DataLength
            || response.Header.Direction > (byte)SerialMonitorDataDirection.Control
            || response.Header.Kind > 1
            || response.Header.Timestamp > (ulong)DateTime.MaxValue.ToFileTimeUtc())
        {
            return false;
        }

        var length = (int)response.Header.DataLength;
        var data = response.Data.Take(length).ToArray();
        record = new SerialMonitorDataRecord(
            DateTime.FromFileTimeUtc((long)response.Header.Timestamp).ToLocalTime(),
            (SerialMonitorDataDirection)response.Header.Direction,
            data)
        {
            Sequence = response.Header.Sequence, RequestId = response.Header.RequestId,
            PortId = response.Header.PortId, Status = response.Header.Status,
            Ioctl = response.Header.Ioctl, RequestedLength = response.Header.RequestedLength,
            TransferredLength = response.Header.TransferredLength, Offset = response.Header.Offset,
            Kind = response.Header.Kind, Flags = response.Header.Flags
        };
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopMonitoring();
        _disposed = true;
    }

    private DriverLifecycleResult EnsureReady()
    {
        if (!IsAdministrator)
        {
            return DriverLifecycleResult.Failure("SerialMonitor 驱动操作需要管理员权限。");
        }

        var lifecycle = _driverLifecycleService.EnsureInstalledAndStarted();
        if (!lifecycle.Succeeded)
        {
            return lifecycle;
        }

        _device ??= _createDevice();
        return _device.IsOpen || _device.Open()
            ? DriverLifecycleResult.Success("SerialMonitor 驱动可用。")
            : DriverLifecycleResult.Failure("SerialMonitor 驱动不可用。");
    }

    private void StartReadLoop()
    {
        StopReadLoop();

        _readLoopCts = new CancellationTokenSource();
        var token = _readLoopCts.Token;
        var sessionDevice = _device;
        _readLoopTask = Task.Run(async () =>
        {
            try
            {
                var lastStatus = Environment.TickCount64;
                while (!token.IsCancellationRequested)
                {
                    var readAny = false;
                    for (var batch = 0; batch < 256 && !token.IsCancellationRequested && TryReadDataFrom(sessionDevice, out var record); batch++)
                    {
                        if (token.IsCancellationRequested) break;
                        readAny = true;
                        DataReceived?.Invoke(this, record!);
                    }

                    if (!token.IsCancellationRequested && Environment.TickCount64 - lastStatus >= 250)
                    {
                        lastStatus = Environment.TickCount64;
                        var status = ReadStatus(sessionDevice);
                        if (token.IsCancellationRequested) break;
                        if (status is null) throw new IOException("无法读取监控状态，捕获已中断。");
                        StatusChanged?.Invoke(this, status);
                        if (status.State != SerialMonitorState.Running)
                        {
                            IsMonitoring = false;
                            ErrorOccurred?.Invoke(this, "目标设备已移除或内核会话已停止，请重新选择端口启动监控。");
                            break;
                        }
                    }

                    if (!readAny)
                    {
                        await Task.Delay(20, token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    sessionDevice?.DeviceIoControl(SerialMonitorInterop.IoctlStopMonitor, null, [], out _);
                    IsMonitoring = false;
                    ErrorOccurred?.Invoke(this, ex.Message);
                }
            }
        }, token);
    }

    private void StopReadLoop()
    {
        if (_readLoopCts is null)
        {
            return;
        }

        _readLoopCts.Cancel();
        _readLoopCts.Dispose();
        _readLoopCts = null;
        _readLoopTask = null;
    }

    private static bool IsCurrentProcessAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string ResolvePortDevicePath(string portName)
    {
        if (portName.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase))
        {
            return portName;
        }

        return SerialMonitorPortIdentity.Resolve(portName);
    }

}

internal interface ISerialMonitorDevice : IDisposable
{
    bool IsOpen { get; }

    int LastError { get; }

    bool Open();

    bool DeviceIoControl(uint ioctl, byte[]? input, byte[] output, out int bytesReturned);
}

internal sealed class Win32SerialMonitorDevice : ISerialMonitorDevice
{
    private readonly object _sync = new();
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;

    private SafeFileHandle? _handle;

    public int LastError { get; private set; }

    public bool IsOpen => _handle is { IsInvalid: false, IsClosed: false };

    public bool Open()
    {
        lock (_sync) return OpenCore();
    }

    private bool OpenCore()
    {
        if (IsOpen)
        {
            return true;
        }

        _handle = CreateFile(
            SerialMonitorInterop.UserModePath,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileAttributeNormal,
            IntPtr.Zero);
        LastError = IsOpen ? 0 : Marshal.GetLastWin32Error();
        return IsOpen;
    }

    public bool DeviceIoControl(uint ioctl, byte[]? input, byte[] output, out int bytesReturned)
    {
        lock (_sync) return DeviceIoControlCore(ioctl, input, output, out bytesReturned);
    }

    private bool DeviceIoControlCore(uint ioctl, byte[]? input, byte[] output, out int bytesReturned)
    {
        bytesReturned = 0;
        var succeeded = IsOpen && DeviceIoControl(
            _handle!,
            ioctl,
            input,
            input?.Length ?? 0,
            output,
            output.Length,
            out bytesReturned,
            IntPtr.Zero);
        LastError = succeeded ? 0 : Marshal.GetLastWin32Error();
        return succeeded;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _handle?.Dispose();
            _handle = null;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        byte[]? lpInBuffer,
        int nInBufferSize,
        byte[] lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);
}

internal static class SerialMonitorInterop
{
    public const int MaxPortNameLength = 64;
    public const int MaxDataSize = 4096;
    public const string UserModePath = @"\\.\SerialMonitor";
    public const uint IoctlStartMonitor = 0x8000E040;
    public const uint IoctlStopMonitor = 0x8000E044;
    public const uint IoctlGetData = 0x80006048;
    public const uint IoctlGetStatus = 0x8000604C;
    public const uint IoctlGetDiagnostics = 0x80006050;

    public static int SizeOf<T>() where T : struct => Marshal.SizeOf<T>();

    public static byte[] Serialize<T>(T value) where T : struct
    {
        var bytes = new byte[SizeOf<T>()];
        WriteStruct(value, bytes);
        return bytes;
    }

    public static void WriteStruct<T>(T value, byte[] destination) where T : struct
    {
        var size = SizeOf<T>();
        if (destination.Length < size)
        {
            throw new ArgumentException("目标缓冲区太小。", nameof(destination));
        }

        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(value, ptr, false);
            Marshal.Copy(ptr, destination, 0, size);
            Marshal.DestroyStructure<T>(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public static T Deserialize<T>(byte[] bytes) where T : struct
    {
        var size = SizeOf<T>();
        if (bytes.Length < size)
        {
            throw new ArgumentException("源缓冲区太小。", nameof(bytes));
        }

        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.Copy(bytes, 0, ptr, size);
            return Marshal.PtrToStructure<T>(ptr)!;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StartMonitorRequest
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxPortNameLength, ArraySubType = UnmanagedType.U2)]
        public ushort[] PortName;

        public static StartMonitorRequest FromPortName(string portName)
        {
            if (portName.Length >= MaxPortNameLength)
            {
                throw new ArgumentException($"串口名称不能超过 {MaxPortNameLength - 1} 个字符。", nameof(portName));
            }

            var chars = portName.Select(static c => (ushort)c).ToArray();
            var buffer = new ushort[MaxPortNameLength];
            Array.Copy(chars, buffer, chars.Length);
            return new StartMonitorRequest { PortName = buffer };
        }

        public readonly string PortNameString()
        {
            var length = Array.IndexOf(PortName, (ushort)0);
            if (length < 0)
            {
                length = PortName.Length;
            }

            return new string(PortName.Take(length).Select(static x => (char)x).ToArray());
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CapturedDataHeader
    {
        public ulong Timestamp;
        public ulong Sequence;
        public ulong RequestId;
        public ulong PortId;
        public int Status;
        public uint Ioctl;
        public uint RequestedLength;
        public uint TransferredLength;
        public uint DataLength;
        public uint Offset;
        public byte Direction;
        public byte Kind;
        public ushort Flags;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GetDataResponse
    {
        public CapturedDataHeader Header;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxDataSize)]
        public byte[] Data;

        public static GetDataResponse From(ulong timestamp, SerialMonitorDataDirection direction, byte[] data)
        {
            if (data.Length > MaxDataSize)
            {
                throw new ArgumentException($"数据长度不能超过 {MaxDataSize} 字节。", nameof(data));
            }

            var buffer = new byte[MaxDataSize];
            Array.Copy(data, buffer, data.Length);
            return new GetDataResponse
            {
                Header = new CapturedDataHeader
                {
                    Timestamp = timestamp,
                    Direction = (byte)direction,
                    RequestedLength = (uint)data.Length,
                    TransferredLength = (uint)data.Length,
                    DataLength = (uint)data.Length
                },
                Data = buffer
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DriverStatus
    {
        public uint Version;
        public uint State;
        public ulong PortId;
        public ulong Generation;
        public ulong TotalBytesRx;
        public ulong TotalBytesTx;
        public ulong DroppedRecords;
        public ulong DroppedBytes;
        public uint CapturedEntryCount;
        public uint AttachedPorts;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DriverDiagnostics
    {
        public uint LastStartStage;
        public int LastStartStatus;
        public uint PnpFilterCount;
        public uint FilterCreateCount;
        public int LastFilterCreateStatus;
    }
}
