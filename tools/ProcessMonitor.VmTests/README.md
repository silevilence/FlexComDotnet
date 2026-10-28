# 专用虚拟机批量验收

仅用于用户授权的专用 COM10↔20、COM11↔21、COM12↔22。无需安装驱动或更改系统安全配置。程序只启动/结束自身测试子进程；运行前确保三个端口对空闲。默认完整批次约 31 分钟，x86/x64 长测并行。

构建生产 Hook 与 NativeTests 后，用 `dotnet publish` 将本项目自包含发布为 win-x64（根目录）和 win-x86（根目录下 worker-x86）。将匹配产物放入 `native/x64` 与 `native/x86`：FlexCom.ProcessHook.dll、FlexCom.ProcessHook.Helper.exe、NativeTests.exe 及许可说明。可选 `app` 目录放置自包含 WPF 包供 UI 验证。

双击根目录 ProcessMonitor.VmTests.exe。启动器复制包到独立 TEMP 目录，隐藏运行工作进程，结果写入原包 `results-yyyyMMdd-HHmmss`。只检查 `finished.txt` 不足以判定支持范围，必须查看 progress.txt、managed JSON 与内存样本。

可选局域网传输：在开发机运行 `python transfer-server.py <artifacts目录> <主机专用接口IP> <虚拟机IP>`，只提供 FlexComVmTests.zip 下载和限定文件名的报告上传。根目录 `report-url.txt` 指向 `http://<主机IP>:8765/report/`。HTTP 仅用于隔离测试网络，不提供通用文件访问或命令执行；结束后停止服务。未配置服务或上传失败时，虚拟机本地报告仍保留。

最终实测报告见 `docs/research/2026-10-28-process-hook-vm-acceptance.md`。当前 .NET SerialPort 场景应失败；不得降低断言以掩盖真实采集缺陷。长测允许已报告丢弃，但严格核对业务调用总数与完成/丢弃总数，另行保留丢弃率。
