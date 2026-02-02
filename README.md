# 🔌 FlexComDotnet

**灵活的串口调试助手** - 一款功能强大的 Windows 串口通信工具

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?logo=windows)
![Tests](https://img.shields.io/badge/Tests-199%20Passed-brightgreen)
![License](https://img.shields.io/badge/License-MIT-green)

## 📋 项目状态

**🚧 开发中 - Phase 2 (高级功能与布局优化)**

当前版本已完成串口基础功能，正在开发多区域可折叠布局。

## ✨ 功能列表

### ✅ 已完成
- [x] **项目初始化** - Feature-first 目录结构、代码检查规则
- [x] **串口配置 UI** - 串口选择、波特率/数据位/停止位/校验位配置、流控支持 (RTS/CTS, XON/XOFF, DTR/DSR)
- [x] **串口核心管理** - 后台串口服务、自动扫描、状态管理与错误处理
- [x] **基础收发功能** - 实时数据接收显示、Hex/ASCII 模式切换、文本/Hex 发送
- [x] **用户配置持久化** - JSON 配置文件自动保存/加载
- [x] **发送辅助工具** - 定时循环发送、自动追加换行符、自动追加校验位 (Checksum/CRC16-MODBUS)
- [x] **视图交互与日志** - 通信日志保存、时间戳显示、Rx/Tx 计数器、暂停滚动/清空接收区
- [x] **多条指令列表** - 预设指令管理 (添加/编辑/删除/拖拽排序)、LiteDB 数据持久化、快速发送

### 🚧 开发中
- [ ] **多区域可折叠布局** - VS Code 风格三区架构 (Left/Right/Bottom)、Activity Bar 导航

### 🔜 计划中
- [ ] 智能自动回复系统 (匹配回复/顺序回复)
- [ ] 独立校验与摘要计算器 (CRC/Checksum/MD5/SHA)
- [ ] 数据可视化与实时示波器
- [ ] 通用帧协议解析引擎
- [ ] Lua 脚本系统
- [ ] TCP/UDP 网络扩展
- [ ] 自动更新功能

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
│   │   │   └── Serial/Views/       # 串口视图 (Config/Communication/CommandList)
│   │   ├── Services/               # DI 服务注册
│   │   ├── App.xaml                # 应用入口
│   │   └── MainWindow.xaml         # 主窗口
│   │
│   └── FlexComDotnet.Core/         # 核心业务层 (无 UI 依赖)
│       └── Features/
│           └── Serial/
│               ├── Helpers/        # 工具类 (Hex/Checksum)
│               ├── Models/         # 数据模型 & 枚举
│               ├── Services/       # 串口/配置/存储服务
│               └── ViewModels/     # MVVM ViewModel
│
└── tests/
    └── FlexComDotnet.Tests/        # 单元测试 (199 个用例)
        └── Features/Serial/        # 串口功能测试
```

### 架构模式

- **MVVM**: 使用 CommunityToolkit.Mvvm 实现视图与业务逻辑分离
- **依赖注入**: 通过 Microsoft.Extensions.DependencyInjection 管理服务生命周期
- **Feature-first**: 按功能模块组织代码，便于扩展和维护
- **TDD**: 测试驱动开发，确保代码质量

## 🛠️ 技术栈

| 类别 | 技术 | 版本 |
|------|------|------|
| 框架 | .NET | 10.0 |
| UI | WPF | - |
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
