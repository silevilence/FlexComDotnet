// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using FlexComDotnet.Core.Features.ProcessMonitor.Services;
using System.IO.MemoryMappedFiles;
using System.Buffers.Binary;

var nativeRoot = Path.GetFullPath(args[0]);
foreach (var architecture in new[] { "x64", "x86" })
{
    var start = new ProcessStartInfo(Path.Combine(nativeRoot, architecture, "NativeTests.exe"), "--host-worker")
    { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, CreateNoWindow = true };
    using var worker = Process.Start(start)!;
    try
    {
        if (await worker.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)) != "READY") throw new Exception("Worker did not start.");
        var targets = new ProcessMonitorTargets(nativeRoot);
        var target = targets.GetTarget(worker.Id);
        if (target.Architecture != architecture) throw new Exception("Wrong architecture.");
        await using var session = new ProcessMonitorSession(new NativeProcessMonitorTransport(nativeRoot));
        await session.AttachAsync(target);
        if (!session.IsMonitoring) throw new Exception("Attach failed.");
        await worker.StandardInput.WriteLineAsync("EMIT");
        if (await worker.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)) != "EMITTED") throw new Exception("Native wire fixture did not run.");
        for (var i = 0; i < 100 && session.Records.Count < 3; i++) await Task.Delay(20);
        var received = session.Records;
        if (received.Count != 3 || received[0].Api != "ReadFile" || received[0].Handle != 0x12345678
            || received[0].RequestedLength != 16 || received[0].CompletedLength != 3
            || !received[0].Data.SequenceEqual(new byte[] { 0, 0x7f, 0xff })
            || received[1].Integrity != FlexComDotnet.Core.Features.Capture.Models.CaptureIntegrity.Gap
            || received[1].DroppedRecords != 2 || !received[2].Data.SequenceEqual(new byte[] { 0x11 }))
            throw new Exception("Native ABI bytes or ordered loss markers did not reach the managed session intact.");
        // A resident DLL must not be initialized using an export offset from a different on-disk build.
        var variantDirectory = Path.Combine(Path.GetTempPath(), "flexcom-hook-build-" + Guid.NewGuid());
        Directory.CreateDirectory(variantDirectory);
        var variantDll = Path.Combine(variantDirectory, "FlexCom.ProcessHook.dll");
        try
        {
            var image = File.ReadAllBytes(Path.Combine(nativeRoot, architecture, "FlexCom.ProcessHook.dll"));
            var timestampOffset = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(0x3c)) + 8;
            var timestamp = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(timestampOffset));
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(timestampOffset), unchecked(timestamp + 1));
            File.WriteAllBytes(variantDll, image);
            var attach = new ProcessStartInfo(Path.Combine(nativeRoot, architecture, "FlexCom.ProcessHook.Helper.exe"))
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "--attach", target.ProcessId.ToString(), target.StartTime.ToString(), variantDll }) attach.ArgumentList.Add(argument);
            using var helper = Process.Start(attach)!;
            await helper.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));
            if (helper.ExitCode == 0 || worker.HasExited) throw new Exception("Resident build mismatch must fail without calling a mismatched export.");
        }
        finally { File.Delete(variantDll); Directory.Delete(variantDirectory); }
        await session.StopAsync();
        if (worker.HasExited) throw new Exception("Stop terminated target.");
        await session.AttachAsync(target);
        // A malformed external producer must disconnect and release ownership without a user Stop.
        var queueName = $"Local\\FlexCom.ProcessHook.v1.{target.ProcessId}.{target.StartTime}";
        using (var mapping = MemoryMappedFile.OpenExisting(queueName))
        using (var view = mapping.CreateViewAccessor())
        using (var mutex = Mutex.OpenExisting(queueName + ".lock"))
        {
            if (!mutex.WaitOne(1000)) throw new Exception("Could not access test queue.");
            try { view.Write(40, 257u); }
            finally { mutex.ReleaseMutex(); }
        }
        for (var i = 0; i < 100 && session.IsMonitoring; i++) await Task.Delay(20);
        if (session.IsMonitoring) throw new Exception("Malformed queue was not disconnected.");
        await using (var recovered = new ProcessMonitorSession(new NativeProcessMonitorTransport(nativeRoot)))
        {
            await recovered.AttachAsync(target);
            if (!recovered.IsMonitoring) throw new Exception("Failed reader retained target ownership.");
            await recovered.StopAsync();
        }
        await session.AttachAsync(target);
        await worker.StandardInput.WriteLineAsync("EXIT");
        await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        for (var i = 0; i < 100 && session.IsMonitoring; i++) await Task.Delay(20);
        if (session.IsMonitoring) throw new Exception("Target exit was not detected.");
        Console.WriteLine($"PASS {architecture}: selection, native attach, stop without termination, reattach, exit");
    }
    finally { if (!worker.HasExited) { worker.StandardInput.Close(); await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); } }
    var launcher = new ProcessMonitorTargets(nativeRoot);
    await using var launch = await launcher.PrepareLaunchAsync(Path.Combine(nativeRoot, architecture, "NativeTests.Gui.exe"), "--launch-worker");
    await using var launchedSession = new ProcessMonitorSession(new NativeProcessMonitorTransport(nativeRoot));
    await launchedSession.StartAsync(launch);
    using var launched = Process.GetProcessById(launch.Target.ProcessId);
    if (!launchedSession.IsMonitoring || launched.HasExited) throw new Exception("Startup capture failed.");
    await launchedSession.StopAsync();
    if (launched.HasExited) throw new Exception("Stop terminated launched target.");
    await launched.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    Console.WriteLine($"PASS {architecture}: suspended startup, arguments, stop, natural exit");
}
