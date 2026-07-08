using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace FlexComDotnet.Core.Features.Driver.Services;

public enum SerialMonitorDataDirection : byte
{
    Tx = 0,
    Rx = 1
}

public enum SerialMonitorState : byte
{
    Stopped = 0,
    Running = 1
}

public sealed record SerialMonitorDataRecord(DateTime Timestamp, SerialMonitorDataDirection Direction, byte[] Data)
{
    public bool IsTx => Direction == SerialMonitorDataDirection.Tx;
}

public sealed record SerialMonitorStatus(
    SerialMonitorState State,
    uint CapturedEntryCount,
    ulong TotalBytesRx,
    ulong TotalBytesTx);

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
    private readonly bool _startReadLoopOnStart;
    private ISerialMonitorDevice? _device;
    private CancellationTokenSource? _readLoopCts;
    private Task? _readLoopTask;
    private bool _disposed;

    public SerialMonitorService()
        : this(new DriverLifecycleService(), () => new Win32SerialMonitorDevice(), IsCurrentProcessAdministrator)
    {
    }

    internal SerialMonitorService(
        DriverLifecycleService driverLifecycleService,
        Func<ISerialMonitorDevice> createDevice,
        Func<bool> isAdministrator,
        bool startReadLoopOnStart = true)
    {
        _driverLifecycleService = driverLifecycleService;
        _createDevice = createDevice;
        _isAdministrator = isAdministrator;
        _startReadLoopOnStart = startReadLoopOnStart;
    }

    public bool IsAdministrator => _isAdministrator();

    public bool IsMonitoring { get; private set; }

    public event EventHandler<SerialMonitorDataRecord>? DataReceived;

    public event EventHandler<string>? ErrorOccurred;

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
        return device.Open()
            ? new SerialMonitorAvailability(true, true, true, "SerialMonitor 驱动可用。")
            : new SerialMonitorAvailability(true, false, false, "SerialMonitor 驱动不可用。");
    }

    public DriverLifecycleResult StartMonitoring(string portName)
    {
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

        var request = SerialMonitorInterop.StartMonitorRequest.FromPortName(portName);
        if (!_device!.DeviceIoControl(
                SerialMonitorInterop.IoctlStartMonitor,
                SerialMonitorInterop.Serialize(request),
                [],
                out _))
        {
            return DriverLifecycleResult.Failure("启动串口监控失败。");
        }

        IsMonitoring = true;
        if (_startReadLoopOnStart)
        {
            StartReadLoop();
        }

        return DriverLifecycleResult.Success("串口监控已启动。");
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
        var ready = EnsureReady();
        if (!ready.Succeeded)
        {
            return null;
        }

        var output = new byte[SerialMonitorInterop.SizeOf<SerialMonitorInterop.DriverStatus>()];
        if (!_device!.DeviceIoControl(SerialMonitorInterop.IoctlGetStatus, null, output, out var bytesReturned)
            || bytesReturned < output.Length)
        {
            return null;
        }

        var status = SerialMonitorInterop.Deserialize<SerialMonitorInterop.DriverStatus>(output);
        return new SerialMonitorStatus(
            (SerialMonitorState)status.State,
            status.CapturedEntryCount,
            status.TotalBytesRx,
            status.TotalBytesTx);
    }

    public bool TryReadData(out SerialMonitorDataRecord? record)
    {
        record = null;

        if (_device is not { IsOpen: true })
        {
            return false;
        }

        var output = new byte[SerialMonitorInterop.SizeOf<SerialMonitorInterop.GetDataResponse>()];
        if (!_device.DeviceIoControl(SerialMonitorInterop.IoctlGetData, null, output, out var bytesReturned)
            || bytesReturned < SerialMonitorInterop.SizeOf<SerialMonitorInterop.CapturedDataHeader>())
        {
            return false;
        }

        var response = SerialMonitorInterop.Deserialize<SerialMonitorInterop.GetDataResponse>(output);
        if (response.Header.DataLength == 0
            || response.Header.DataLength > SerialMonitorInterop.MaxDataSize
            || response.Header.Direction > (byte)SerialMonitorDataDirection.Rx)
        {
            return false;
        }

        var length = (int)response.Header.DataLength;
        var data = response.Data.Take(length).ToArray();
        record = new SerialMonitorDataRecord(
            DateTime.FromFileTimeUtc((long)response.Header.Timestamp).ToLocalTime(),
            (SerialMonitorDataDirection)response.Header.Direction,
            data);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopReadLoop();
        _device?.Dispose();
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
        _readLoopTask = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var readAny = false;
                    while (TryReadData(out var record))
                    {
                        readAny = true;
                        DataReceived?.Invoke(this, record!);
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
                ErrorOccurred?.Invoke(this, ex.Message);
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
}

internal interface ISerialMonitorDevice : IDisposable
{
    bool IsOpen { get; }

    bool Open();

    bool DeviceIoControl(uint ioctl, byte[]? input, byte[] output, out int bytesReturned);
}

internal sealed class Win32SerialMonitorDevice : ISerialMonitorDevice
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;

    private SafeFileHandle? _handle;

    public bool IsOpen => _handle is { IsInvalid: false, IsClosed: false };

    public bool Open()
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

        return IsOpen;
    }

    public bool DeviceIoControl(uint ioctl, byte[]? input, byte[] output, out int bytesReturned)
    {
        bytesReturned = 0;
        return IsOpen && DeviceIoControl(
            _handle!,
            ioctl,
            input,
            input?.Length ?? 0,
            output,
            output.Length,
            out bytesReturned,
            IntPtr.Zero);
    }

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
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
    public const uint IoctlStartMonitor = 0x8000E000;
    public const uint IoctlStopMonitor = 0x8000E004;
    public const uint IoctlGetData = 0x80006008;
    public const uint IoctlGetStatus = 0x8000600C;

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
        public byte Direction;
        public byte Reserved1;
        public ushort Reserved2;
        public uint DataLength;
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
                    DataLength = (uint)data.Length
                },
                Data = buffer
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DriverStatus
    {
        public byte State;
        public byte Reserved1;
        public ushort Reserved2;
        public uint CapturedEntryCount;
        public ulong TotalBytesRx;
        public ulong TotalBytesTx;
    }
}
