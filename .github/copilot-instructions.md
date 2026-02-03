# FlexComDotnet Project Guidelines

## 1. 项目概况
- **项目名称**: FlexComDotnet
- **核心语言**: C#
- **框架版本**: .NET 10
- **UI 框架**: WPF (Windows Presentation Foundation) + Panuon.WPF.UI
- **架构模式**: MVVM
- **核心库**: CommunityToolkit.Mvvm

## 2. 目录结构
```
FlexComDotnet/
├── .github/
│   ├── prompts/                  # AI Prompt 模板
│   └── copilot-instructions.md   # Copilot 开发指南
├── src/
│   ├── FlexComDotnet/            # WPF UI 层
│   │   ├── Converters/           # XAML 值转换器
│   │   ├── Features/
│   │   │   ├── Layout/Controls/  # 布局控件 (ActivityBar, CollapsiblePanel, FloatingPanelWindow, MultiZoneLayout)
│   │   │   └── Serial/Views/     # 串口视图 (Config/Communication/CommandList)
│   │   ├── Services/             # UI 层服务 (ThemeService, ServiceCollectionExtensions)
│   │   ├── Themes/               # 主题资源字典 (DarkTheme.xaml, LightTheme.xaml)
│   │   ├── App.xaml(.cs)         # 应用入口
│   │   └── MainWindow.xaml(.cs)  # 主窗口
│   │
│   └── FlexComDotnet.Core/       # 核心业务层 (无 UI 依赖)
│       └── Features/
│           ├── Layout/
│           │   ├── Models/       # 布局状态模型 (LayoutState, PanelInfo, PanelZone)
│           │   └── Services/     # 面板管理器 (IPanelManager)
│           └── Serial/
│               ├── Helpers/      # 工具类 (HexHelper, ChecksumHelper)
│               ├── Models/       # 数据模型 & 枚举 (AppConfig, SerialPortConfig, CommandItem)
│               ├── Services/     # 串口/配置/存储服务 (ISerialPortService, IConfigurationService, ICommandStorageService)
│               └── ViewModels/   # MVVM ViewModel (SerialConfigViewModel, SerialCommunicationViewModel, CommandListViewModel)
│
└── tests/
    └── FlexComDotnet.Tests/      # 单元测试 (236 个用例)
        └── Features/
            ├── Layout/           # 布局功能测试
            └── Serial/           # 串口功能测试
```

**关键文件说明：**
- `README.md`: 项目说明文档
- `ROADMAP.md`: 功能开发计划与进度
- `FlexComDotnet.slnx`: 解决方案文件
- `.editorconfig`: 代码风格配置
- `.gitignore`: Git 忽略文件

## 3. 常用命令 (Dotnet CLI)
所有命令默认在项目根目录下执行。

### 构建与运行
- **构建项目**: `dotnet build`
- **运行应用**: `dotnet run --project src/FlexComDotnet/FlexComDotnet.csproj`
- **清理项目**: `dotnet clean`
- **构建无警告**: `dotnet build --warnaserror`

### 测试
- **运行所有测试**: `dotnet test`
- **运行特定测试项目**: `dotnet test tests/FlexComDotnet.Tests/FlexComDotnet.Tests.csproj`
- **测试覆盖率**: `dotnet test --collect:"XPlat Code Coverage"`

### 依赖管理
- **添加 NuGet 包**: `dotnet add src/FlexComDotnet.Core/FlexComDotnet.Core.csproj package [PackageName]`
- **移除 NuGet 包**: `dotnet remove src/FlexComDotnet.Core/FlexComDotnet.Core.csproj package [PackageName]`
- **列出依赖**: `dotnet list package`

### 项目管理
- **创建新解决方案**: `dotnet new sln`
- **添加项目到解决方案**: `dotnet sln add [项目路径]`
- **检查 SDK 版本**: `dotnet --version`
- **清理 NuGet 缓存**: `dotnet nuget locals all --clear`

## 4. 开发流程 (Development Workflow)

### TDD 原则 (Test-Driven Development)
在编写任何功能代码之前，**必须**遵循 TDD 流程：
1. **Red**: 编写一个失败的测试用例，定义预期的行为。
2. **Green**: 编写最少量的代码让测试通过。
3. **Refactor**: 在保证测试通过的前提下优化代码结构。

### Bug 修复流程
1. 创建一个能够复现 Bug 的测试用例（此时测试应失败）。
2. 修复代码。
3. 验证测试通过。

### 交付标准
- **完成检查**: 在提示用户尝试或提交代码片段之前，**必须**运行相关测试并确保通过 (`dotnet test`)。

## 5. Agent 行为准则 (Behavior Guidelines)

### CLI First 原则
- **依赖管理**: 添加/移除 NuGet 包**必须**使用 `dotnet add/remove package` 命令，**严禁**直接编辑 `.csproj` 文件。
- **项目操作**: 创建项目、添加引用等操作优先使用 `dotnet` CLI。

### Git 操作
- **被动模式**: 禁止主动执行 git commit, push, pull 等操作。
- **显式触发**: 仅在用户明确使用相关 Skill 或指令要求进行版本控制操作时执行。

### 文档更新
- **被动模式**: 禁止主动更新 `ROADMAP.md` 或其他文档文件。
- **显式触发**: 仅在用户明确要求更新文档任务状态或计划时执行。

## 6. 项目状态与技术栈

### 技术栈详情
**核心框架：**
- **.NET SDK**: 10.0.102 (通过 global.json 配置)
- **目标框架**: net10.0-windows
- **UI 框架**: WPF (UseWPF=true) + Panuon.WPF.UI 1.3.0.2
- **架构模式**: MVVM with CommunityToolkit.Mvvm

**核心功能库：**
- **串口通信**: System.IO.Ports 10.0.2
- **WMI 查询**: System.Management 10.0.2
- **本地存储**: LiteDB 5.0.21 (文档数据库)
- **脚本引擎**: NLua 1.7.8 (Lua 脚本支持)
- **配置管理**: Microsoft.Extensions.Configuration.Json 10.0.2
- **依赖注入**: Microsoft.Extensions.DependencyInjection 10.0.2
- **MVVM 工具包**: CommunityToolkit.Mvvm 8.4.0

**开发工具：**
- **测试框架**: xUnit 2.9.3
- **断言库**: FluentAssertions 8.8.0
- **模拟框架**: Moq 4.20.72
- **代码覆盖率**: coverlet.collector 6.0.4

### 代码质量配置
- **可为空引用类型**: 启用
- **隐式 using**: 启用
- **语言版本**: latest
- **代码分析**: 启用 .NET 分析器，警告视为错误
- **代码风格**: 通过 .editorconfig 配置

### 开发环境要求
1. **.NET SDK**: 10.0.102 或更高版本
2. **操作系统**: Windows 10/11 (WPF 应用)
3. **开发工具**:
   - VS Code with C# Dev Kit
4. **Git**: 版本控制

### 项目进度跟踪
- **ROADMAP.md**: 功能开发计划与优先级
- **README.md**: 项目说明和快速开始指南
- **Git 提交**: 遵循 Emoji 提交规范
- **测试覆盖**: 所有功能必须包含单元测试

## 7. 开发最佳实践

### 命名规范
- **项目命名**: `FlexComDotnet.{Layer}` (如 Core, Tests)
- **命名空间**: `FlexComDotnet.{Layer}.Features.{Feature}.{SubFolder}`
- **接口**: `I{Name}` (如 `ISerialPortService`)
- **ViewModel**: `{Feature}ViewModel` (如 `SerialConfigViewModel`)
- **测试类**: `{ClassName}Tests` (如 `SerialPortServiceTests`)

### 功能开发流程
1. **需求分析**: 参考 ROADMAP.md 确定功能优先级
2. **TDD 实施**: 先写测试，再实现功能
3. **模块化设计**: 在 Features/ 目录下创建功能模块
4. **依赖注入**: 通过 DI 容器注册服务
5. **UI 绑定**: 使用 MVVM 模式分离业务逻辑和界面
6. **测试覆盖**: 确保所有公共 API 都有测试
7. **文档更新**: 更新相关文档和示例

### 代码组织原则
1. **单一职责**: 每个类/方法只做一件事
2. **开放封闭**: 对扩展开放，对修改封闭
3. **依赖倒置**: 依赖抽象，不依赖具体实现
4. **接口隔离**: 客户端不应依赖不需要的接口
5. **里氏替换**: 子类可以替换父类

### 主题系统开发规范
- **DynamicResource**: UI 元素颜色必须使用 `{DynamicResource BrushName}` 绑定
- **主题资源**: 新增颜色需同时在 `DarkTheme.xaml` 和 `LightTheme.xaml` 中定义
- **控件样式**: 全局控件样式覆盖定义在主题文件底部
- **命名规范**: 颜色用 `{Name}Color`，画刷用 `{Name}Brush`

### 错误处理策略
1. **结构化异常**: 使用 try-catch-finally
2. **日志记录**: 记录详细错误信息和上下文
3. **用户反馈**: 提供友好的错误提示
4. **资源清理**: 确保异常时资源正确释放
5. **恢复机制**: 提供错误恢复选项

### 性能优化指南
1. **异步操作**: 使用 async/await 避免 UI 阻塞
2. **内存管理**: 及时释放非托管资源
3. **数据绑定**: 使用 ObservableCollection 优化 UI 更新
4. **缓存策略**: 合理缓存频繁访问的数据
5. **延迟加载**: 按需加载资源和数据
