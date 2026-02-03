# 🔌 FlexComDotnet

**灵活的通信调试助手** - 一款功能强大的 Windows 串口/网络通信工具

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?logo=windows)
![Tests](https://img.shields.io/badge/Tests-585%20Passed-brightgreen)
![License](https://img.shields.io/badge/License-MIT-green)

## 📋 项目状态

**🚧 开发中 - Phase 7 (P2 高级功能扩展)**

当前版本已完成串口基础功能、多区域布局、主题系统、独立校验计算器、CI/CD 自动发布、智能自动回复系统、网络扩展 (TCP/UDP)、自动更新功能，下一步开发数据可视化与协议解析引擎。

## ✨ 功能列表

### ✅ 已完成
- [x] **项目初始化** - Feature-first 目录结构、代码检查规则
- [x] **串口配置 UI** - 串口选择、波特率/数据位/停止位/校验位配置、流控支持 (RTS/CTS, XON/XOFF, DTR/DSR)
- [x] **串口核心管理** - 后台串口服务、自动扫描、状态管理与错误处理
- [x] **基础收发功能** - 实时数据接收显示、Hex/ASCII 模式切换、文本/Hex 发送
- [x] **用户配置持久化** - JSON 配置文件自动保存/加载 (串口、网络配置均支持)
- [x] **发送辅助工具** - 定时循环发送、自动追加换行符、自动追加校验位 (Checksum/CRC16-MODBUS)
- [x] **视图交互与日志** - 通信日志保存、时间戳显示(支持日期切换)、暂停滚动/清空接收区
- [x] **状态栏** - Rx/Tx 字节计数器、图标式重置按钮
- [x] **多条指令列表** - 预设指令管理 (添加/编辑/删除/拖拽排序)、LiteDB 数据持久化、快速发送
- [x] **多区域可折叠布局** - VS Code 风格三区架构 (Left/Right/Bottom)、Activity Bar 导航、面板拖拽移动
- [x] **主题系统** - 浅色/深色/跟随系统三种模式、科技风格 UI (Panuon.WPF.UI)、主题设置持久化
- [x] **独立校验与摘要计算器** - 策略模式架构，支持 Sum8/16、CRC-8/16/32 多种变体、XOR、MD5、SHA-1/256；Hex 输入带 ASCII 预览，可导入/附加发送帧
- [x] **CI/CD 自动发布** - GitHub Actions 自动构建、打包 (.zip/.msix)、发布 Release
- [x] **智能自动回复系统** - 匹配回复 (Hex/Ascii 特征码触发)、顺序回复 (循环帧列表)、策略模式架构、配置自动保存
- [x] **网络扩展** - 统一连接接口 (IConnection)、TCP Client/Server 模式、UDP 单播/广播收发、配置持久化
- [x] **自动更新** - GitHub API 版本检测、语义版本号比对、下载进度显示、安装包唤起

### 🔜 计划中
- [ ] 数据可视化与实时示波器
- [ ] 通用帧协议解析引擎
- [ ] Lua 脚本系统

## 🚀 快速开始

### 环境要求
- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.102+)

### 构建与运行

```bash
# 克隆项目
git clone https://github.com/silevilence/FlexComDotnet.git
cd FlexComDotnet

# 还原依赖
dotnet restore

# 构建项目
dotnet build

# 运行应用
dotnet run --project src/FlexComDotnet/FlexComDotnet.csproj

# 运行测试
dotnet test
```

## 🏗️ 项目架构

```
FlexComDotnet/
├── src/
│   ├── FlexComDotnet/              # WPF UI 层
│   │   ├── Converters/             # XAML 值转换器
│   │   ├── Features/
│   │   │   ├── AutoReply/Views/    # 自动回复视图
│   │   │   ├── Checksum/Views/     # 校验计算器视图
│   │   │   ├── Layout/Controls/    # 布局控件 (ActivityBar, CollapsiblePanel 等)
│   │   │   ├── Network/Views/      # 网络连接视图 (ConnectionConfigView)
│   │   │   └── Serial/Views/       # 串口视图 (Config/Communication/CommandList)
│   │   ├── Services/               # UI 层服务 (主题服务)
│   │   ├── Themes/                 # 主题资源 (Light/Dark)
│   │   ├── App.xaml                # 应用入口
│   │   └── MainWindow.xaml         # 主窗口 (含状态栏)
│   │
│   └── FlexComDotnet.Core/         # 核心业务层 (无 UI 依赖)
│       └── Features/
│           ├── AutoReply/          # 自动回复功能
│           │   ├── Models/         # 配置模型 (MatchRule, SequentialFrame, ReplyMode)
│           │   ├── Services/       # 策略模式处理器 (IReplyHandler, MatchReplyHandler, SequentialReplyHandler)
│           │   └── ViewModels/     # 自动回复 ViewModel
│           ├── Checksum/           # 校验计算器功能
│           │   ├── Models/         # 算法枚举
│           │   ├── Services/       # 策略模式算法实现
│           │   └── ViewModels/     # 计算器 ViewModel
│           ├── Layout/             # 布局功能
│           │   ├── Models/         # 布局状态模型
│           │   └── Services/       # 面板管理器
│           ├── Network/            # 网络功能
│           │   ├── Models/         # 连接模型 (ConnectionType, ConnectionState, NetworkConfig)
│           │   ├── Services/       # 连接服务 (IConnection, ITcpClientService, ITcpServerService, IUdpService)
│           │   └── ViewModels/     # 连接配置 ViewModel
│           ├── Serial/             # 串口功能
│           │   ├── Helpers/        # 工具类 (Hex/Checksum)
│           │   ├── Models/         # 数据模型 & 枚举
│           │   ├── Services/       # 串口/配置/存储服务
│           │   └── ViewModels/     # MVVM ViewModel
│           └── Update/             # 自动更新功能
│               ├── Models/         # 版本/发布信息 (VersionInfo, ReleaseInfo, UpdateCheckResult)
│               ├── Services/       # 更新服务 (IUpdateService, IVersionService, IGitHubReleaseService)
│               └── ViewModels/     # 更新 ViewModel
│
└── tests/
    └── FlexComDotnet.Tests/        # 单元测试 (585 个用例)
        └── Features/
            ├── AutoReply/          # 自动回复测试
            ├── Checksum/           # 校验计算器测试
            ├── Layout/             # 布局功能测试
            ├── Network/            # 网络功能测试
            ├── Serial/             # 串口功能测试
            └── Update/             # 自动更新测试
```

### 架构模式

- **MVVM**: 使用 CommunityToolkit.Mvvm 实现视图与业务逻辑分离
- **依赖注入**: 通过 Microsoft.Extensions.DependencyInjection 管理服务生命周期
- **Feature-first**: 按功能模块组织代码，便于扩展和维护
- **TDD**: 测试驱动开发，确保代码质量
- **主题系统**: DynamicResource 绑定实现运行时主题切换

## 🛠️ 技术栈

| 类别 | 技术 | 版本 |
|------|------|------|
| 框架 | .NET | 10.0 |
| UI | WPF + Panuon.WPF.UI | 1.3.0.2 |
| MVVM | CommunityToolkit.Mvvm | 8.4.0 |
| 串口 | System.IO.Ports | 10.0.2 |
| WMI | System.Management | 10.0.2 |
| DI | Microsoft.Extensions.DependencyInjection | 10.0.2 |
| 配置 | Microsoft.Extensions.Configuration.Json | 10.0.2 |
| 存储 | LiteDB | 5.0.21 |
| 脚本 | NLua | 1.7.8 |
| 测试 | xUnit 2.9.3 + FluentAssertions 8.8.0 + Moq 4.20.72 | - |

## 📖 开发指南

详见 [.github/copilot-instructions.md](.github/copilot-instructions.md)

### TDD 开发流程

1. **Red**: 先编写失败的测试
2. **Green**: 编写最少代码让测试通过
3. **Refactor**: 重构优化代码

### 常用命令

```bash
# 构建
dotnet build

# 测试
dotnet test

# 添加 NuGet 包
dotnet add src/FlexComDotnet.Core/FlexComDotnet.Core.csproj package [PackageName]
```

## 📜 License

MIT License - 详见 [LICENSE](LICENSE)
