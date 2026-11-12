using System.Diagnostics;
using System.Xml.Linq;
using FluentAssertions;

namespace FlexComDotnet.Tests.Build;

public class ProcessHookBuildTests
{
    [Fact]
    public async Task BuildTarget_ShouldRunBothLocalScriptsWithIsolatedPowerShellEnvironment()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository != null && !File.Exists(Path.Combine(repository.FullName, "src", "FlexCom.ProcessHook", "ProcessHook.targets")))
            repository = repository.Parent;
        repository.Should().NotBeNull("the integration test imports the actual build target");

        var directory = Path.Combine(Path.GetTempPath(), "FlexCom Build Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var target = Path.Combine(repository!.FullName, "src", "FlexCom.ProcessHook", "ProcessHook.targets");
            var project = Path.Combine(directory, "test.proj");
            new XDocument(new XElement("Project", new XElement("Import", new XAttribute("Project", target)))).Save(project);
            // Simulate an incompatible module inherited from the shell that launched MSBuild.
            var modules = Path.Combine(directory, "Modules");
            var securityModule = Path.Combine(modules, "Microsoft.PowerShell.Security");
            Directory.CreateDirectory(securityModule);
            await File.WriteAllTextAsync(Path.Combine(securityModule, "Microsoft.PowerShell.Security.psm1"), """
                function Get-ExecutionPolicy { throw 'Incompatible parent-shell module was loaded.' }
                Export-ModuleMember -Function Get-ExecutionPolicy
                """);
            await File.WriteAllTextAsync(Path.Combine(directory, "Build-Hook.ps1"), """
                param([string]$Architecture)
                $ErrorActionPreference = 'Stop'
                "$Architecture|$(Get-ExecutionPolicy)" | Add-Content -LiteralPath (Join-Path $PSScriptRoot 'invocations.txt')
                """);
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add("msbuild");
            start.ArgumentList.Add(project);
            start.ArgumentList.Add("-nologo");
            start.ArgumentList.Add("-t:BuildProcessHookAssets");
            start.ArgumentList.Add("-p:ProcessHookSource=" + directory + Path.DirectorySeparatorChar);
            // Reproduce Windows PowerShell's default restriction without changing machine/user settings.
            start.Environment["PSExecutionPolicyPreference"] = "Restricted";
            start.Environment["PSModulePath"] = modules;
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
            process.ExitCode.Should().Be(0, "both MSBuild script invocations must succeed: {0}\n{1}", await output, await errors);
            File.ReadAllLines(Path.Combine(directory, "invocations.txt"))
                .Should().Equal("x86|RemoteSigned", "x64|RemoteSigned");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
