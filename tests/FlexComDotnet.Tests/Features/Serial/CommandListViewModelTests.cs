using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;
using FlexComDotnet.Core.Features.Serial.ViewModels;
using FluentAssertions;
using Moq;

namespace FlexComDotnet.Tests.Features.Serial;

/// <summary>
/// CommandListViewModel 测试
/// </summary>
public class CommandListViewModelTests : IDisposable
{
    private readonly Mock<ICommandStorageService> _mockStorageService;
    private readonly Mock<ISerialPortService> _mockSerialPortService;
    private readonly CommandListViewModel _viewModel;

    public CommandListViewModelTests()
    {
        _mockStorageService = new Mock<ICommandStorageService>();
        _mockSerialPortService = new Mock<ISerialPortService>();

        // 设置默认返回空列表
        _mockStorageService.Setup(s => s.GetAll()).Returns(new List<CommandItem>().AsReadOnly());

        _viewModel = new CommandListViewModel(_mockStorageService.Object, _mockSerialPortService.Object);
    }

    public void Dispose()
    {
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }

    #region 初始化测试

    [Fact]
    public void Constructor_ShouldInitializeWithEmptyCommands()
    {
        // Assert
        _viewModel.Commands.Should().BeEmpty();
        _viewModel.SelectedCommand.Should().BeNull();
        _viewModel.IsEditing.Should().BeFalse();
    }

    [Fact]
    public void Constructor_ShouldLoadCommandsFromStorage()
    {
        // Arrange
        var commands = new List<CommandItem>
        {
            new() { Id = 1, Name = "Command 1", Content = "AA BB" },
            new() { Id = 2, Name = "Command 2", Content = "CC DD" }
        };
        var mockStorage = new Mock<ICommandStorageService>();
        mockStorage.Setup(s => s.GetAll()).Returns(commands.AsReadOnly());
        var mockSerial = new Mock<ISerialPortService>();

        // Act
        using var vm = new CommandListViewModel(mockStorage.Object, mockSerial.Object);

        // Assert
        vm.Commands.Should().HaveCount(2);
        vm.Commands[0].Name.Should().Be("Command 1");
        vm.Commands[1].Name.Should().Be("Command 2");
    }

    #endregion

    #region 添加指令测试

    [Fact]
    public void AddCommand_ShouldEnterEditingMode()
    {
        // Act
        _viewModel.AddCommandCommand.Execute(null);

        // Assert
        _viewModel.IsEditing.Should().BeTrue();
        _viewModel.IsCreating.Should().BeTrue();
        _viewModel.EditName.Should().BeEmpty();
        _viewModel.EditContent.Should().BeEmpty();
    }

    [Fact]
    public void SaveEdit_WhenCreating_ShouldAddNewCommand()
    {
        // Arrange
        _mockStorageService.Setup(s => s.Add(It.IsAny<CommandItem>())).Returns(1);
        
        _viewModel.AddCommandCommand.Execute(null);
        _viewModel.EditName = "Test Command";
        _viewModel.EditContent = "01 02 03";
        _viewModel.EditIsHexMode = true;

        // Act
        _viewModel.SaveEditCommand.Execute(null);

        // Assert
        _viewModel.IsEditing.Should().BeFalse();
        _viewModel.Commands.Should().HaveCount(1);
        _viewModel.Commands[0].Name.Should().Be("Test Command");
        _viewModel.Commands[0].Content.Should().Be("01 02 03");
        _viewModel.Commands[0].IsHexMode.Should().BeTrue();
        _mockStorageService.Verify(s => s.Add(It.IsAny<CommandItem>()), Times.Once);
    }

    [Fact]
    public void SaveEdit_WhenNameIsEmpty_ShouldNotExecute()
    {
        // Arrange
        _viewModel.AddCommandCommand.Execute(null);
        _viewModel.EditName = "";
        _viewModel.EditContent = "AA BB";

        // Assert
        _viewModel.SaveEditCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SaveEdit_WhenContentIsEmpty_ShouldNotExecute()
    {
        // Arrange
        _viewModel.AddCommandCommand.Execute(null);
        _viewModel.EditName = "Test";
        _viewModel.EditContent = "";

        // Assert
        _viewModel.SaveEditCommand.CanExecute(null).Should().BeFalse();
    }

    #endregion

    #region 编辑指令测试

    [Fact]
    public void EditCommand_ShouldPopulateEditFields()
    {
        // Arrange
        var command = new CommandItem { Id = 1, Name = "Test", Content = "AA", Description = "Desc", IsHexMode = true };
        _mockStorageService.Setup(s => s.GetAll()).Returns(new List<CommandItem> { command }.AsReadOnly());
        
        using var vm = new CommandListViewModel(_mockStorageService.Object, _mockSerialPortService.Object);
        vm.SelectedCommand = vm.Commands[0];

        // Act
        vm.EditCommandCommand.Execute(null);

        // Assert
        vm.IsEditing.Should().BeTrue();
        vm.IsCreating.Should().BeFalse();
        vm.EditName.Should().Be("Test");
        vm.EditContent.Should().Be("AA");
        vm.EditDescription.Should().Be("Desc");
        vm.EditIsHexMode.Should().BeTrue();
    }

    [Fact]
    public void SaveEdit_WhenEditing_ShouldUpdateExistingCommand()
    {
        // Arrange
        var command = new CommandItem { Id = 1, Name = "Original", Content = "AA" };
        _mockStorageService.Setup(s => s.GetAll()).Returns(new List<CommandItem> { command }.AsReadOnly());
        _mockStorageService.Setup(s => s.Update(It.IsAny<CommandItem>())).Returns(true);
        
        using var vm = new CommandListViewModel(_mockStorageService.Object, _mockSerialPortService.Object);
        vm.SelectedCommand = vm.Commands[0];
        vm.EditCommandCommand.Execute(null);
        
        vm.EditName = "Updated";
        vm.EditContent = "BB CC";

        // Act
        vm.SaveEditCommand.Execute(null);

        // Assert
        vm.IsEditing.Should().BeFalse();
        vm.Commands[0].Name.Should().Be("Updated");
        vm.Commands[0].Content.Should().Be("BB CC");
        _mockStorageService.Verify(s => s.Update(It.IsAny<CommandItem>()), Times.Once);
    }

    #endregion

    #region 删除指令测试

    [Fact]
    public void DeleteCommand_WhenSelected_ShouldRemoveCommand()
    {
        // Arrange
        var command = new CommandItem { Id = 1, Name = "Test", Content = "AA" };
        _mockStorageService.Setup(s => s.GetAll()).Returns(new List<CommandItem> { command }.AsReadOnly());
        _mockStorageService.Setup(s => s.Delete(1)).Returns(true);
        
        using var vm = new CommandListViewModel(_mockStorageService.Object, _mockSerialPortService.Object);
        vm.SelectedCommand = vm.Commands[0];

        // Act
        vm.DeleteCommandCommand.Execute(null);

        // Assert
        vm.Commands.Should().BeEmpty();
        vm.SelectedCommand.Should().BeNull();
        _mockStorageService.Verify(s => s.Delete(1), Times.Once);
    }

    [Fact]
    public void DeleteCommand_WhenNoSelection_CannotExecute()
    {
        // Assert
        _viewModel.DeleteCommandCommand.CanExecute(null).Should().BeFalse();
    }

    #endregion

    #region 发送指令测试

    [Fact]
    public void SendCommand_WhenNotConnected_CannotExecute()
    {
        // Arrange
        var command = new CommandItem { Id = 1, Name = "Test", Content = "AA", IsEnabled = true };
        _mockStorageService.Setup(s => s.GetAll()).Returns(new List<CommandItem> { command }.AsReadOnly());
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(false);
        
        using var vm = new CommandListViewModel(_mockStorageService.Object, _mockSerialPortService.Object);
        vm.SelectedCommand = vm.Commands[0];

        // Assert
        vm.SendCommandCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SendCommand_WhenConnectedAndSelected_ShouldRaiseSendEvent()
    {
        // Arrange
        var command = new CommandItem { Id = 1, Name = "Test", Content = "Hello", IsEnabled = true, IsHexMode = false };
        _mockStorageService.Setup(s => s.GetAll()).Returns(new List<CommandItem> { command }.AsReadOnly());
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        
        using var vm = new CommandListViewModel(_mockStorageService.Object, _mockSerialPortService.Object);
        
        // 模拟连接状态变化
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        
        vm.SelectedCommand = vm.Commands[0];

        byte[]? sentData = null;
        vm.SendDataRequested += (_, data) => sentData = data;

        // Act
        vm.SendCommandCommand.Execute(null);

        // Assert
        sentData.Should().NotBeNull();
        sentData.Should().Equal(System.Text.Encoding.UTF8.GetBytes("Hello"));
    }

    [Fact]
    public void SendCommand_WhenDisabled_CannotExecute()
    {
        // Arrange
        var command = new CommandItem { Id = 1, Name = "Test", Content = "AA", IsEnabled = false };
        _mockStorageService.Setup(s => s.GetAll()).Returns(new List<CommandItem> { command }.AsReadOnly());
        _mockSerialPortService.Setup(s => s.IsConnected).Returns(true);
        
        using var vm = new CommandListViewModel(_mockStorageService.Object, _mockSerialPortService.Object);
        _mockSerialPortService.Raise(s => s.ConnectionStateChanged += null, _mockSerialPortService.Object, true);
        vm.SelectedCommand = vm.Commands[0];

        // Assert
        vm.SendCommandCommand.CanExecute(null).Should().BeFalse();
    }

    #endregion

    #region 排序测试

    [Fact]
    public void MoveUp_WhenNotFirstItem_ShouldMoveItemUp()
    {
        // Arrange
        var commands = new List<CommandItem>
        {
            new() { Id = 1, Name = "First", SortOrder = 1 },
            new() { Id = 2, Name = "Second", SortOrder = 2 }
        };
        _mockStorageService.Setup(s => s.GetAll()).Returns(commands.AsReadOnly());
        _mockStorageService.Setup(s => s.UpdateSortOrder(It.IsAny<IEnumerable<CommandItem>>())).Returns(true);
        
        using var vm = new CommandListViewModel(_mockStorageService.Object, _mockSerialPortService.Object);
        vm.SelectedCommand = vm.Commands[1]; // Select "Second"

        // Act
        vm.MoveUpCommand.Execute(null);

        // Assert
        vm.Commands[0].Name.Should().Be("Second");
        vm.Commands[1].Name.Should().Be("First");
    }

    [Fact]
    public void MoveUp_WhenFirstItem_CannotExecute()
    {
        // Arrange
        var commands = new List<CommandItem> { new() { Id = 1, Name = "First" } };
        _mockStorageService.Setup(s => s.GetAll()).Returns(commands.AsReadOnly());
        
        using var vm = new CommandListViewModel(_mockStorageService.Object, _mockSerialPortService.Object);
        vm.SelectedCommand = vm.Commands[0];

        // Assert
        vm.MoveUpCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void MoveDown_WhenNotLastItem_ShouldMoveItemDown()
    {
        // Arrange
        var commands = new List<CommandItem>
        {
            new() { Id = 1, Name = "First", SortOrder = 1 },
            new() { Id = 2, Name = "Second", SortOrder = 2 }
        };
        _mockStorageService.Setup(s => s.GetAll()).Returns(commands.AsReadOnly());
        _mockStorageService.Setup(s => s.UpdateSortOrder(It.IsAny<IEnumerable<CommandItem>>())).Returns(true);
        
        using var vm = new CommandListViewModel(_mockStorageService.Object, _mockSerialPortService.Object);
        vm.SelectedCommand = vm.Commands[0]; // Select "First"

        // Act
        vm.MoveDownCommand.Execute(null);

        // Assert
        vm.Commands[0].Name.Should().Be("Second");
        vm.Commands[1].Name.Should().Be("First");
    }

    [Fact]
    public void MoveDown_WhenLastItem_CannotExecute()
    {
        // Arrange
        var commands = new List<CommandItem> { new() { Id = 1, Name = "First" } };
        _mockStorageService.Setup(s => s.GetAll()).Returns(commands.AsReadOnly());
        
        using var vm = new CommandListViewModel(_mockStorageService.Object, _mockSerialPortService.Object);
        vm.SelectedCommand = vm.Commands[0];

        // Assert
        vm.MoveDownCommand.CanExecute(null).Should().BeFalse();
    }

    #endregion

    #region 取消编辑测试

    [Fact]
    public void CancelEdit_ShouldExitEditingMode()
    {
        // Arrange
        _viewModel.AddCommandCommand.Execute(null);
        _viewModel.EditName = "Test";
        _viewModel.EditContent = "AA BB";

        // Act
        _viewModel.CancelEditCommand.Execute(null);

        // Assert
        _viewModel.IsEditing.Should().BeFalse();
    }

    [Fact]
    public void CancelEdit_WhenNotEditing_CannotExecute()
    {
        // Assert
        _viewModel.CancelEditCommand.CanExecute(null).Should().BeFalse();
    }

    #endregion
}
