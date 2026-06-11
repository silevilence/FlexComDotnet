# 🔌 FlexComDotnet

**灵活的通信调试助手** - 一款功能强大的 Windows 串口/网络通信工具

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?logo=windows)
![Tests](https://img.shields.io/badge/Tests-1059_passing-brightgreen)
![License](https://img.shields.io/badge/License-MIT-green)
![Rust](https://img.shields.io/badge/Driver-Rust_%2F_WDK-orange?logo=rust)

## 📋 项目状态

**🚀 开发中 - 驱动安装生命周期管理与串口传输控制优化**

当前版本 v1.5.0。已完成串口/网络通信、协议解析引擎（通用/DL/T 645-2007/Modbus-RTU）、Lua 脚本系统、智能自动回复（规则池架构）、数据可视化、协议组帧与解析联动、Rust 内核驱动框架等核心功能。当前正在开发驱动安装与生命周期管理、串口帧定界与传输控制、自动响应防抖机制。

## ✨ 功能列表

### ✅ 已完成

**通信核心**
- [x] **串口通信** - 串口配置 (波特率/数据位/停止位/校验位/流控)、自动扫描、实时收发 (Hex/ASCII)、定时循环发送、自动追加换行/校验位
- [x] **网络扩展** - 统一连接接口 (IConnection)、TCP Client/Server、UDP 单播/广播
- [x] **多条指令列表** - 预设指令管理 (添加/编辑/删除/拖拽排序)、LiteDB 持久化、快速发送

**协议解析**
- [x] **通用帧协议引擎** - 策略模式架构 (IProtocolParser)、可配置字段提取 (字节/位域/端序)、帧结构定义 UI
- [x] **DL/T 645-2007 解析器** - 粘包处理、BCD 地址解析、控制码解析、数据域偏置还原、数据标识字典映射、异常应答翻译
- [x] **协议组帧** - 基于数据域值动态构建完整帧、发送区快捷组帧回填 (覆盖/追加)
- [x] **Rx 协议逆向解析** - 接收区选中帧右键触发解析、独立非模态浮窗展示

**自动回复**
- [x] **统一规则池架构** - 匹配规则/顺序帧/协议回复三种类型统一管理、多选并发触发、优先级执行
- [x] **匹配回复** - Hex/Ascii 特征码触发、协议级断言 (字段条件 + 关系运算符)、动态上下文变量提取
- [x] **顺序回复** - 多套独立规则配置、循环控制
- [x] **协议回复** - 协议选择器绑定、数据项插值表达式、动态表单渲染
- [x] **响应载荷** - 纯文本手动输入 / 协议动态组帧双模式切换

**脚本系统**
- [x] **Lua 脚本引擎** - 脚本管理器、FCom API 桥接 (send/log/delay/crc/checksum/getTimestamp)
- [x] **Hook 机制** - 接收预处理 (Rx)、发送后处理 (Tx)、自动应答 (Reply)、手动任务 (Task)
- [x] **脚本编辑器增强** - 语法高亮 (AvalonEdit)、智能补全 (含协议名/数据项上下文感知)、API 参考文档
- [x] **协议对象 API** - 脚本内检索/实例化协议、parse/build 编解码方法、静态语法/依赖检查
- [x] **安全管理** - Hook 配置持久化 (启用状态不持久化)、脚本删除引用校验、协议修改/删除拦截

**数据可视化**
- [x] **实时示波器** - ScottPlot 多通道绘制、暂停/缩放/十字游标、通道管理、PNG/CSV 导出
- [x] **数据源绑定** - 与协议解析引擎联动，从解析字段中选择 Y 轴数据

**界面与交互**
- [x] **多区域布局** - VS Code 风格三区架构 (Left/Right/Bottom)、标签式展开/折叠、面板可见性独立配置
- [x] **主题系统** - 浅色/深色/跟随系统、Panuon.WPF.UI 科技风格
- [x] **彩色 Emoji** - Emoji.Wpf 渲染引擎、`:shortcode:` 智能补全 (IntelliSense)
- [x] **设置窗口** - 调试设置/面板管理/日志目录/关于信息、F12 调试工具窗口
- [x] **统一日志** - 多来源标注、等级分类 (emoji 前缀)、按日期持久化、多维度筛选

**工程基础**
- [x] **校验与摘要计算器** - Sum8/16、CRC-8/16/32 多变体、XOR、MD5、SHA-1/256；Hex 输入带 ASCII 预览
- [x] **CI/CD** - GitHub Actions 自动构建、打包 (.zip/.msix)、CHANGELOG 驱动发布
- [x] **自动更新** - GitHub API 版本检测、语义版本号比对、下载进度显示、应用内更新
- [x] **配置持久化** - JSON 配置自动保存/加载 (串口/网络/自动回复/Hook/面板布局)

**协议解析扩展**
- [x] **Modbus-RTU 协议支持** - 从站地址/功能码配置、基于功能码的动态表单、CRC-16 自动编解码、Lua 脚本无缝接入
- [x] **协议组帧/逆向解析** - Rx 接收区右键协议逆向解析、Tx 发送区快捷组帧回填 (覆盖/追加)
- [x] **协议管理依赖图谱** - 修改/删除协议时自动检查脚本依赖并拦截

**驱动开发**
- [x] **Rust 内核串口监控驱动** - Windows 内核驱动框架 (WDK)、过滤驱动 IRP 拦截、IOCTL 用户态通信、共享数据结构

**接收区优化**
- [x] **接收区延迟格式化** - 仅对可见项执行格式化，大幅提升大数据量下接收性能
- [x] **自动滚动** - 新数据到达自动滚动到底部，手动上滚暂停跟随
- [x] **自动换行修复** - 开关正常控制 TextWrapping，联动水平滚动条

### 🚧 开发中
- [ ] **驱动安装与生命周期管理** - 应用启动自动检测/安装/启动驱动，安全与权限控制
- [ ] **串口帧定界与传输控制** - 帧间隔超时判定、最大帧长度限制、运行期生命周期控制
- [ ] **自动响应防抖机制** - 防抖延迟窗口、多帧联合决策 (AND/OR/LAST/FIRST 模式)

## 🚀 快速开始

### 环境要求
- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.102+)

### 构建与运行

```bash
# 克隆项目
git clone https://github.com/silevilence/FlexComDotnet.git
cd FlexComDotnet

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
│   │   │   ├── Checksum/Views/     # 校验计算器窗口
│   │   │   ├── EmojiSupport/       # Emoji 补全与渲染控件
│   │   │   ├── Layout/Controls/    # 布局控件 (ActivityBar, CollapsiblePanel, MultiZoneLayout)
│   │   │   ├── Logging/Views/      # 日志面板视图
│   │   │   ├── Network/Views/      # 网络连接视图
│   │   │   ├── Protocol/Views/     # 协议视图 (定义/Rx解析/Tx组帧)
│   │   │   ├── Scripting/          # 脚本 (补全/语法高亮/编辑器/API参考)
│   │   │   ├── Serial/Views/       # 串口视图 (Config/Communication/CommandList)
│   │   │   ├── Settings/Views/     # 设置与调试窗口
│   │   │   ├── Update/Views/       # 更新窗口
│   │   │   └── Visualization/Views/# 数据可视化视图
│   │   ├── Fonts/                  # 内嵌字体 (MapleMono-NF-CN)
│   │   ├── Services/               # UI 层服务 (主题服务、DI 配置)
│   │   └── Themes/                 # 主题资源 (Light/Dark)
│   │
│   └── FlexComDotnet.Core/         # 核心业务层 (无 UI 依赖)
│       └── Features/
│           ├── AutoReply/          # Models / Services (Handlers/) / ViewModels
│           ├── Checksum/           # Models / Services (Algorithms/) / ViewModels
│           ├── EmojiSupport/       # Models / Services
│           ├── Layout/             # Models / Services
│           ├── Logging/            # Models / Services / ViewModels
│           ├── Network/            # Models / Services / ViewModels
│           ├── Protocol/           # Models (Dlt645/) / Services (Parsers/) / ViewModels
│           ├── Scripting/          # Models / Services / ViewModels
│           ├── Serial/             # Helpers / Models / Services / ViewModels
│           ├── Settings/           # Models / ViewModels
│           ├── Update/             # Models / Services / ViewModels
│           └── Visualization/      # Models / Services / ViewModels
│
└── tests/
    └── FlexComDotnet.Tests/        # 单元测试 (xUnit + FluentAssertions + Moq)
        └── Features/               # 按功能模块对应测试
│
├── driver/
│   └── serial-monitor/             # Rust 串口监控内核驱动
│       ├── src/                    # 驱动源码 (entry, device, filter, ioctl, ring_buffer)
│       ├── tests/                  # 集成测试
│       ├── Cargo.toml              # Rust 项目配置 (cargo-make + WDK)
│       └── serial_monitor.inx      # 驱动安装清单
│
└── docs/                           # 文档 (架构/驱动/开发/部署说明)
```

### 架构模式

- **MVVM**: CommunityToolkit.Mvvm 实现视图与业务逻辑分离
- **依赖注入**: Microsoft.Extensions.DependencyInjection 管理服务生命周期
- **Feature-first**: 按功能模块组织代码，Core 层无 UI 依赖
- **策略模式**: 校验算法、协议解析器、自动回复处理器均可零耦合扩展
- **C# ↔ Rust 跨语言通信**: WPF 应用与内核驱动通过 IOCTL + 共享结构体 (`#[repr(C)]`) 双向通信
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
| 代码编辑器 | AvalonEdit | 6.3.1.120 |
| 图表库 | ScottPlot.WPF | 5.1.57 |
| Emoji | Emoji.Wpf | 0.3.4 |
| 测试 | xUnit 2.9.3 + FluentAssertions 8.8.0 + Moq 4.20.72 | - |

## 📖 开发指南

详见 [.github/copilot-instructions.md](.github/copilot-instructions.md)

### 常用命令

```bash
dotnet build                    # 构建
dotnet test                     # 运行所有测试
dotnet test --filter "FullyQualifiedName~ClassName"  # 运行特定测试
dotnet add src/FlexComDotnet.Core/FlexComDotnet.Core.csproj package [PackageName]  # 添加包
```

## 📜 License

MIT License - 详见 [LICENSE](LICENSE)
