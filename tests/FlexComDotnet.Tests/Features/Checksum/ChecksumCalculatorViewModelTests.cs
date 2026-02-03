using FluentAssertions;
using FlexComDotnet.Core.Features.Checksum.Models;
using FlexComDotnet.Core.Features.Checksum.Services;
using FlexComDotnet.Core.Features.Checksum.ViewModels;

namespace FlexComDotnet.Tests.Features.Checksum;

/// <summary>
/// 校验和计算器 ViewModel 测试
/// </summary>
public class ChecksumCalculatorViewModelTests
{
    private readonly ChecksumCalculatorViewModel _viewModel;

    public ChecksumCalculatorViewModelTests()
    {
        var checksumService = new ChecksumService();
        _viewModel = new ChecksumCalculatorViewModel(checksumService);
    }

    #region 初始化测试

    [Fact]
    public void Constructor_ShouldLoadAlgorithms()
    {
        _viewModel.AvailableAlgorithms.Should().NotBeEmpty();
        _viewModel.SelectedAlgorithm.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldSelectFirstAlgorithm()
    {
        _viewModel.SelectedAlgorithm.Should().Be(_viewModel.AvailableAlgorithms.First());
    }

    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        _viewModel.InputText.Should().BeEmpty();
        _viewModel.ResultHex.Should().BeEmpty();
        _viewModel.ResultDecimal.Should().BeEmpty();
    }

    #endregion

    #region 计算测试

    [Fact]
    public void Calculate_WithHexInput_ShouldCalculateCorrectly()
    {
        // Arrange
        _viewModel.SelectedAlgorithm = _viewModel.AvailableAlgorithms
            .First(a => a.Type == ChecksumAlgorithmType.Sum8);
        _viewModel.InputText = "01 02 03";

        // Act
        _viewModel.CalculateCommand.Execute(null);

        // Assert - 0x01 + 0x02 + 0x03 = 0x06
        _viewModel.ResultHex.Should().Be("06");
        _viewModel.ResultDecimal.Should().Be("6");
    }

    [Fact]
    public void Calculate_WithCompactHexInput_ShouldCalculateCorrectly()
    {
        // Arrange
        _viewModel.SelectedAlgorithm = _viewModel.AvailableAlgorithms
            .First(a => a.Type == ChecksumAlgorithmType.Sum8);
        _viewModel.InputText = "414243"; // "ABC" in hex

        // Act
        _viewModel.CalculateCommand.Execute(null);

        // Assert - 0x41 + 0x42 + 0x43 = 0xC6
        _viewModel.ResultHex.Should().Be("C6");
    }

    [Fact]
    public void Calculate_WithEmptyInput_ShouldCalculate()
    {
        // Arrange
        _viewModel.SelectedAlgorithm = _viewModel.AvailableAlgorithms
            .First(a => a.Type == ChecksumAlgorithmType.Sum8);
        _viewModel.InputText = "";

        // Act
        _viewModel.CalculateCommand.Execute(null);

        // Assert
        _viewModel.ResultHex.Should().Be("00");
    }

    [Fact]
    public void Calculate_WithCrc16Modbus_ShouldReturnCorrectResult()
    {
        // Arrange
        _viewModel.SelectedAlgorithm = _viewModel.AvailableAlgorithms
            .First(a => a.Type == ChecksumAlgorithmType.Crc16Modbus);
        _viewModel.InputText = "01 03 00 00 00 0A";

        // Act
        _viewModel.CalculateCommand.Execute(null);

        // Assert - CRC = 0xCDC5, 小端序 = C5 CD
        _viewModel.ResultHex.Should().Be("C5 CD");
    }

    #endregion

    #region 预览测试

    [Fact]
    public void AsciiPreview_WithValidHexInput_ShouldShowAscii()
    {
        // Arrange
        _viewModel.InputText = "48 65 6C 6C 6F"; // "Hello"

        // Assert
        _viewModel.AsciiPreview.Should().Be("Hello");
    }

    [Fact]
    public void AsciiPreview_WithNonPrintable_ShouldReplaceDot()
    {
        // Arrange
        _viewModel.InputText = "00 41 1F"; // NUL, A, Unit Separator

        // Assert
        _viewModel.AsciiPreview.Should().Be(".A.");
    }

    #endregion

    #region 清空测试

    [Fact]
    public void ClearInput_ShouldResetAllFields()
    {
        // Arrange
        _viewModel.InputText = "01 02 03";
        _viewModel.CalculateCommand.Execute(null);

        // Act
        _viewModel.ClearInputCommand.Execute(null);

        // Assert
        _viewModel.InputText.Should().BeEmpty();
        _viewModel.ResultHex.Should().BeEmpty();
        _viewModel.ResultDecimal.Should().BeEmpty();
        _viewModel.ErrorMessage.Should().BeEmpty();
    }

    #endregion

    #region 算法切换测试

    [Fact]
    public void SelectedAlgorithmChanged_ShouldUpdateAlgorithmInfo()
    {
        // Arrange
        var md5Algorithm = _viewModel.AvailableAlgorithms
            .First(a => a.Type == ChecksumAlgorithmType.Md5);

        // Act
        _viewModel.SelectedAlgorithm = md5Algorithm;

        // Assert
        _viewModel.AlgorithmInfo.Should().Contain("MD5");
        _viewModel.AlgorithmInfo.Should().Contain("16 字节");
    }

    [Fact]
    public void SelectedAlgorithmChanged_WithInput_ShouldRecalculate()
    {
        // Arrange
        _viewModel.SelectedAlgorithm = _viewModel.AvailableAlgorithms
            .First(a => a.Type == ChecksumAlgorithmType.Sum8);
        _viewModel.InputText = "01 02 03";
        _viewModel.CalculateCommand.Execute(null);
        var sum8Result = _viewModel.ResultHex;

        // Act - 切换到 XOR 算法
        _viewModel.SelectedAlgorithm = _viewModel.AvailableAlgorithms
            .First(a => a.Type == ChecksumAlgorithmType.Xor);

        // Assert - 结果应该不同
        // XOR: 0x01 ^ 0x02 ^ 0x03 = 0x00
        _viewModel.ResultHex.Should().Be("00");
        _viewModel.ResultHex.Should().NotBe(sum8Result);
    }

    #endregion

    #region 事件测试

    [Fact]
    public void AppendResultToSendFrame_WithResult_ShouldRaiseEvent()
    {
        // Arrange
        byte[]? appendedBytes = null;
        _viewModel.AppendToSendFrameRequested += (_, bytes) => appendedBytes = bytes;
        
        _viewModel.SelectedAlgorithm = _viewModel.AvailableAlgorithms
            .First(a => a.Type == ChecksumAlgorithmType.Sum8);
        _viewModel.InputText = "01 02 03";
        _viewModel.CalculateCommand.Execute(null);

        // Act
        _viewModel.AppendResultToSendFrameCommand.Execute(null);

        // Assert
        appendedBytes.Should().NotBeNull();
        appendedBytes.Should().Equal([0x06]);
    }

    [Fact]
    public void AppendResultToSendFrame_WithoutResult_ShouldSetError()
    {
        // Arrange
        _viewModel.ResultHex = string.Empty;

        // Act
        _viewModel.AppendResultToSendFrameCommand.Execute(null);

        // Assert
        _viewModel.ErrorMessage.Should().NotBeEmpty();
    }

    [Fact]
    public void ImportFromSendFrame_WithHexData_ShouldSetInputAndCalculate()
    {
        // Arrange
        _viewModel.SelectedAlgorithm = _viewModel.AvailableAlgorithms
            .First(a => a.Type == ChecksumAlgorithmType.Sum8);

        // Act
        _viewModel.ImportFromSendFrame("AA BB CC", isHex: true);

        // Assert
        _viewModel.InputText.Should().Be("AA BB CC");
        _viewModel.ResultHex.Should().NotBeEmpty();
    }

    [Fact]
    public void ImportFromSendFrame_WithAsciiData_ShouldConvertToHexAndCalculate()
    {
        // Arrange
        _viewModel.SelectedAlgorithm = _viewModel.AvailableAlgorithms
            .First(a => a.Type == ChecksumAlgorithmType.Sum8);

        // Act - 导入 ASCII "ABC"
        _viewModel.ImportFromSendFrame("ABC", isHex: false);

        // Assert - 应该转换为 Hex: 41 42 43
        _viewModel.InputText.Should().Be("41 42 43");
        _viewModel.ResultHex.Should().NotBeEmpty();
    }

    [Fact]
    public void CopyResult_WithResult_ShouldRaiseEvent()
    {
        // Arrange
        string? copiedText = null;
        _viewModel.CopyToClipboardRequested += (_, text) => copiedText = text;
        
        _viewModel.InputText = "01 02 03";
        _viewModel.CalculateCommand.Execute(null);

        // Act
        _viewModel.CopyResultCommand.Execute(null);

        // Assert
        copiedText.Should().NotBeNull();
    }

    [Fact]
    public void PasteFromClipboard_ShouldRaiseEventAndSetInput()
    {
        // Arrange
        _viewModel.PasteFromClipboardRequested += (_, callback) => callback("AB CD");

        // Act
        _viewModel.PasteFromClipboardCommand.Execute(null);

        // Assert
        _viewModel.InputText.Should().Be("AB CD");
    }

    #endregion
}
