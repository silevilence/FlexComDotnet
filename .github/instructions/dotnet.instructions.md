---
applyTo: "src/**,tests/**"
---

# C# / WPF / MVVM 开发规范

## 1. 目录结构
```
src/
├── FlexComDotnet/            # WPF UI 层
│   ├── Converters/           # XAML 值转换器 (Serial, Network, Protocol, Script, Log)
│   ├── Features/
│   │   ├── AutoReply/Views/
│   │   ├── Checksum/Views/
│   │   ├── EmojiSupport/     # Behaviors/ + Controls/ (Emoji 补全与渲染)
│   │   ├── Layout/Controls/  # ActivityBar, CollapsiblePanel, FloatingPanelWindow, MultiZoneLayout
│   │   ├── Logging/Views/
│   │   ├── Network/Views/
│   │   ├── Protocol/Views/   # ProtocolDefinitionWindow, RxProtocolParseWindow, TxProtocolBuildWindow
│   │   ├── Scripting/        # Completion/ + Resources/ + Views/
│   │   ├── Serial/Views/     # SerialConfigView, SerialCommunicationView, CommandListView
│   │   ├── Settings/Views/   # SettingsWindow, DebugToolsWindow
│   │   ├── Update/Views/
│   │   └── Visualization/Views/
│   ├── Fonts/                # MapleMono-NF-CN
│   ├── Services/             # ThemeService, ServiceCollectionExtensions (DI 注册)
│   └── Themes/               # DarkTheme.xaml, LightTheme.xaml
│
└── FlexComDotnet.Core/       # 核心业务层 (无 UI 依赖)
    └── Features/
        ├── AutoReply/        # Models / Services (Handlers/) / ViewModels
        ├── Checksum/         # Models / Services (Algorithms/) / ViewModels
        ├── EmojiSupport/     # Models / Services
        ├── Layout/           # Models / Services
        ├── Logging/          # Models / Services / ViewModels
        ├── Network/          # Models / Services / ViewModels
        ├── Protocol/         # Models (Dlt645/) / Services (Parsers/) / ViewModels
        ├── Scripting/        # Models / Services / ViewModels
        ├── Serial/           # Helpers / Models / Services / ViewModels
        ├── Settings/         # Models / ViewModels
        ├── Update/           # Models / Services / ViewModels
        └── Visualization/    # Models / Services / ViewModels

tests/
└── FlexComDotnet.Tests/      # 单元测试 (xUnit + FluentAssertions + Moq)
    └── Features/             # 按功能模块对应测试
```

## 2. 常用命令
```bash
dotnet build                           # 构建
dotnet run --project src/FlexComDotnet/FlexComDotnet.csproj  # 运行
dotnet test                            # 运行所有测试
dotnet test --filter "FullyQualifiedName~ClassName"         # 特定测试类
dotnet test --filter "FullyQualifiedName~Class.Method"      # 特定测试方法
dotnet add src/FlexComDotnet.Core/FlexComDotnet.Core.csproj package [Name]  # 添加包
dotnet remove src/FlexComDotnet.Core/FlexComDotnet.Core.csproj package [Name]  # 移除包
```

## 3. 技术栈

**核心依赖:**
- .NET 10.0.102 | WPF (net10.0-windows) | Panuon.WPF.UI 1.3.0.2
- CommunityToolkit.Mvvm 8.4.0 | Microsoft.Extensions.DependencyInjection 10.0.2
- System.IO.Ports 10.0.2 | System.Management 10.0.2 | LiteDB 5.0.21
- NLua 1.7.8 | AvalonEdit 6.3.1.120 | ScottPlot.WPF 5.1.57 | Emoji.Wpf 0.3.4

**测试:** xUnit 2.9.3 + FluentAssertions 8.8.0 + Moq 4.20.72

**环境:** Windows 10/11 | Nullable enable | 隐式 using | .NET 分析器启用

## 4. 命名规范与主题系统

### 命名规范
- **命名空间**: `FlexComDotnet.{Layer}.Features.{Feature}.{SubFolder}`
- **接口**: `I{Name}` | **ViewModel**: `{Feature}ViewModel` | **测试类**: `{ClassName}Tests`
- **私有字段**: `_camelCase` | **静态字段**: `s_camelCase`
- **XAML 缩进**: 2 空格 | **C# 缩进**: 4 空格

### 主题系统
- UI 元素颜色必须使用 `{DynamicResource BrushName}` 绑定
- 新增颜色需同时在 `DarkTheme.xaml` 和 `LightTheme.xaml` 中定义
- 命名: 颜色用 `{Name}Color`，画刷用 `{Name}Brush`

## 5. 策略模式应用规范

当功能涉及多种可互换算法或行为时（校验算法、协议解析、回复规则），采用策略模式：
1. 定义 `I{Feature}Algorithm` / `I{Feature}Handler` 接口
2. 每种算法/行为作为独立类实现接口，放在 `Services/Algorithms/`、`Services/Handlers/` 或 `Services/Parsers/`
3. 通过枚举管理策略映射，`I{Feature}Service` 作为策略选择器
4. 扩展时只需添加实现类 + 注册枚举，无需修改 ViewModel 或 UI

**已有策略实例:** `IChecksumAlgorithm` (11 种算法)、`IReplyHandler` (匹配/顺序/协议/脚本)、`IProtocolParser` (通用/DL\T645/Modbus-RTU)

## 6. 测试规范

- 测试文件: `tests/FlexComDotnet.Tests/Features/{Feature}/{ClassName}Tests.cs`
- 测试方法命名: `{Method}_When{Condition}_Should{ExpectedBehavior}` 或 `{Method}_Should{ExpectedBehavior}`
- 使用 Arrange-Act-Assert 模式，FluentAssertions 断言，Moq 模拟依赖

## 7. 依赖注入配置

- 服务注册: `src/FlexComDotnet/Services/ServiceCollectionExtensions.cs`
- **Singleton**: 服务类 | **Transient**: ViewModel 类

## 8. 注意事项

- **跨线程 UI 更新**: 必须使用 `Application.Current.Dispatcher.Invoke(() => { ... })`
- **CommunityToolkit.Mvvm**: 使用 `[ObservableProperty]` 生成属性通知，`[RelayCommand]` 生成命令
