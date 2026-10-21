using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace FlexComDotnet.Core.Features.Driver.Services;

/// <summary>Resolves a COM alias to the PDO name without opening the serial port.</summary>
internal static class SerialMonitorPortIdentity
{
    internal readonly record struct PortDevice(string PortName, string InstanceId);

    internal static string? ResolveInstanceId(string portName) =>
        ResolveInstanceId(portName, EnumeratePresentPorts());

    internal static string? ResolveInstanceId(string portName, IEnumerable<PortDevice> devices)
    {
        if (string.IsNullOrWhiteSpace(portName)) return null;
        foreach (var device in devices)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(device.PortName, portName))
                return device.InstanceId;
        }
        return null;
    }

    private static IEnumerable<PortDevice> EnumeratePresentPorts()
    {
        // PortName is the actual COM alias. Virtual drivers need not expose
        // Win32_SerialPort or put "(COMx)" in a friendly display name.
        const uint PresentAllClasses = 0x02 | 0x04;
        var set = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, PresentAllClasses);
        if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            for (uint index = 0; ; index++)
            {
                var info = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
                if (!SetupDiEnumDeviceInfo(set, index, ref info))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == 259) yield break; // ERROR_NO_MORE_ITEMS
                    throw new Win32Exception(error);
                }

                const uint GlobalScope = 1, DeviceKey = 1, ReadKey = 0x20019;
                var handle = SetupDiOpenDevRegKey(set, ref info, GlobalScope, 0, DeviceKey, ReadKey);
                if (handle == new IntPtr(-1)) continue;
                using var key = RegistryKey.FromHandle(new SafeRegistryHandle(handle, ownsHandle: true));
                if (key.GetValue("PortName") is not string name || !name.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                    continue;
                var instance = new StringBuilder(512);
                if (!SetupDiGetDeviceInstanceId(set, ref info, instance, (uint)instance.Capacity, out _))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                yield return new PortDevice(name, instance.ToString());
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }

    public static string Resolve(string portName)
    {
        var instance = DriverLifecycleService.ResolvePnpInstanceId(portName);
        if (instance is null) throw new Win32Exception(1168, $"找不到 {portName} 的 PnP 设备实例。");
        var set = SetupDiCreateDeviceInfoList(IntPtr.Zero, IntPtr.Zero);
        if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var info = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
            if (!SetupDiOpenDeviceInfo(set, instance, IntPtr.Zero, 0, ref info))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var bytes = new byte[512];
            const uint PhysicalDeviceObjectName = 0x0E;
            if (!SetupDiGetDeviceRegistryProperty(set, ref info, PhysicalDeviceObjectName,
                    out _, bytes, (uint)bytes.Length, out var count))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return Encoding.Unicode.GetString(bytes, 0, checked((int)count)).TrimEnd('\0');
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfo { public uint Size; public Guid ClassGuid; public uint DevInst; public UIntPtr Reserved; }
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string? enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref DeviceInfo info);
    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiOpenDevRegKey(IntPtr set, ref DeviceInfo info, uint scope, uint profile, uint keyType, uint access);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstanceId(IntPtr set, ref DeviceInfo info, StringBuilder instance, uint size, out uint required);
    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiCreateDeviceInfoList(IntPtr classGuid, IntPtr parent);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiOpenDeviceInfo(IntPtr set, string instance, IntPtr parent, uint flags, ref DeviceInfo info);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr set, ref DeviceInfo info, uint property,
        out uint type, [Out] byte[] buffer, uint size, out uint required);
    [DllImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
}
