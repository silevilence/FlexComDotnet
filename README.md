# 🔌 FlexComDotnet

**灵活的串口调试助手** - 一款功能强大的 Windows 串口通信工具

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?logo=windows)
![License](https://img.shields.io/badge/License-MIT-green)

## 📋 项目状态

**🚧 开发中 - Phase 1 (串口基础功能)**

当前版本正在实现串口核心管理和配置 UI。

## ✨ 功能列表

### ✅ 已完成
- [x] 项目初始化与架构搭建
- [x] Feature-first 目录结构
- [x] 串口配置数据模型 (波特率/数据位/停止位/校验位/流控)
- [x] 串口服务接口与实现 (扫描/打开/关闭/收发)
- [x] 串口配置 UI (下拉选择/刷新/连接状态指示)
- [x] MVVM 架构 (ViewModel + 数据绑定)
- [x] 依赖注入配置
- [x] 单元测试覆盖 (54 个测试用例)

### 🔜 计划中
- [ ] 基础收发功能 (接收区/发送区)
- [ ] 用户配置持久化 (JSON)
- [ ] 多区域可折叠布局
- [ ] 通信日志与数据导出
- [ ] 定时循环发送
- [ ] 智能自动回复系统
- [ ] 校验计算器
- [ ] 通用帧协议解析引擎
- [ ] Lua 脚本系统
- [ ] TCP/UDP 网络扩展

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
│   │   ├── Converters/             # 值转换器
│   │   ├── Features/
│   │   │   └── Serial/Views/       # 串口配置视图
│   │   ├── Services/               # DI 配置
│   │   ├── App.xaml                # 应用入口
│   │   └── MainWindow.xaml         # 主窗口
│   │
│   └── FlexComDotnet.Core/         # 核心业务层
│       └── Features/
│           └── Serial/
│               ├── Models/         # 数据模型 & 枚举
│               ├── Services/       # 串口服务接口与实现
│               └── ViewModels/     # MVVM ViewModel
│
└── tests/
    └── FlexComDotnet.Tests/        # 单元测试
        └── Features/Serial/        # 串口功能测试
```

### 架构模式

- **MVVM**: 使用 CommunityToolkit.Mvvm 实现视图与业务逻辑分离
- **依赖注入**: 通过 Microsoft.Extensions.DependencyInjection 管理服务生命周期
- **Feature-first**: 按功能模块组织代码，便于扩展和维护

## 🛠️ 技术栈

| 类别 | 技术 | 版本 |
|------|------|------|
| 框架 | .NET | 10.0 |
| UI | WPF | - |
| MVVM | CommunityToolkit.Mvvm | 8.4.0 |
| 串口 | System.IO.Ports | 10.0.2 |
| DI | Microsoft.Extensions.DependencyInjection | 10.0.2 |
| 存储 | LiteDB | 5.0.21 |
| 脚本 | NLua | 1.7.8 |
| 测试 | xUnit + FluentAssertions + Moq | latest |

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
dotnet add src/FlexComDotnet.Core package [PackageName]
```

## 📜 License

MIT License - 详见 [LICENSE](LICENSE)
