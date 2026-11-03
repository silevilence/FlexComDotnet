# 专用虚拟机批量验收

仅用于用户授权的专用 COM10↔20、COM11↔21、COM12↔22。无需安装驱动或更改系统安全配置。程序只启动/结束自身测试子进程；运行前确保三个端口对空闲。默认完整批次约 36 分钟，包含 30 分钟采集与 5 分钟停止后内存观测，x86/x64 长测并行。

构建生产 Hook 与 NativeTests 后，用 `dotnet publish` 将本项目自包含发布为 win-x64（根目录）和 win-x86（根目录下 worker-x86）。将匹配产物放入 `native/x64` 与 `native/x86`：FlexCom.ProcessHook.dll、FlexCom.ProcessHook.Helper.exe、NativeTests.exe 及许可说明。可选 `app` 目录放置自包含 WPF 包供 UI 验证。

双击根目录 ProcessMonitor.VmTests.exe。启动器复制包到独立 TEMP 目录，隐藏运行工作进程，结果写入原包 `results-yyyyMMdd-HHmmss`。只检查 `finished.txt` 不足以判定支持范围，必须查看 progress.txt、managed JSON 与内存样本。

可选局域网传输：在开发机运行 `python transfer-server.py <artifacts目录> <主机专用接口IP> <虚拟机IP>`，只提供 FlexComVmTests.zip 下载和限定文件名的报告上传。根目录 `report-url.txt` 指向 `http://<主机IP>:8765/report/`。HTTP 仅用于隔离测试网络，不提供通用文件访问或命令执行；结束后停止服务。未配置服务或上传失败时，虚拟机本地报告仍保留。

历史失败报告保留在 `docs/research/2026-10-28-process-hook-vm-acceptance.md`，本轮诊断和修复见 `docs/research/2026-11-03-process-hook-fixes.md`。运行中附加的 .NET SerialPort 场景仍可能失败；不得降低断言或从报告删除失败样本。

包根目录的可选标记：

- `managed-stress.txt`：除默认附加单轮外，增加启动单轮及附加/启动各 200 轮双向并发。压力载荷编码方向和轮次，校验字节、实际长度、请求去重及收发句柄映射。
- `managed-only.txt`：只运行 .NET 测试，跳过原生串口和长期性能测试。
- `diagnostic-baseline.txt`：将长测缩短为 30 秒，并允许已报告丢弃，用于修复前诊断；不能当成最终验收。

正常长测严格要求零丢弃、零错字节、零重复，受控同步工作负载满足业务完成数＝采集完成数＋丢弃数。吞吐比和 API p99 同时报告短测及长测相对同次短无 Hook 基线的变化，长测也执行阈值断言。虚拟串口结果包含调度影响，不代表物理线路性能。`memory-*.json` 与 `stopped-memory-*.json` 记录内存、历史容量与淘汰，须结合趋势检查，不能仅根据程序退出码宣称内存有界。

复跑构建示例（从仓库根目录执行，先构建两个架构原生组件；输出选择全新目录，避免沿用旧标记或混用 DLL）：

```powershell
dotnet publish tools/ProcessMonitor.VmTests -c Release -r win-x64 --self-contained true -o artifacts/hook-run
dotnet publish tools/ProcessMonitor.VmTests -c Release -r win-x86 --self-contained true -o artifacts/hook-run/worker-x86
foreach ($architecture in @('x64', 'x86')) {
    $destination = "artifacts/hook-run/native/$architecture"
    New-Item -ItemType Directory -Path $destination | Out-Null
    foreach ($filename in @('FlexCom.ProcessHook.dll', 'FlexCom.ProcessHook.Helper.exe', 'NativeTests.exe', 'Detours-LICENSE.md')) {
        Copy-Item -LiteralPath "src/FlexCom.ProcessHook/artifacts/$architecture/$filename" -Destination $destination
    }
}
Set-Content -LiteralPath artifacts/hook-run/managed-stress.txt -Value '200 rounds'
```

将完整目录复制至专用虚拟机或通过 RDP 驱动器映射打开，再双击根目录的验收程序。探针等待控制命令最多 10 分钟，以覆盖五分钟停止观察；不会因采集停止而主动关闭目标。
