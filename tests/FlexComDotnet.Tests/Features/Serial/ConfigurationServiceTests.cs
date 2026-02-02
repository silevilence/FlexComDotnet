using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Serial;

public class ConfigurationServiceTests : IDisposable
{
    private readonly string _testConfigPath;
    private readonly JsonConfigurationService _service;

    public ConfigurationServiceTests()
    {
        // 使用临时目录避免污染实际配置
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"flexcom_test_{Guid.NewGuid()}", "config.json");
        _service = new JsonConfigurationService(_testConfigPath);
    }

    public void Dispose()
    {
        // 清理测试文件
        var directory = Path.GetDirectoryName(_testConfigPath);
        if (directory != null && Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Load_WhenConfigFileNotExists_ReturnsDefaultConfig()
    {
        // Act
        var config = _service.Load();

        // Assert
        config.Should().NotBeNull();
        config.SerialConfig.Should().NotBeNull();
        config.SerialConfig.BaudRate.Should().Be(BaudRate.Baud115200);
        config.SerialConfig.DataBits.Should().Be(DataBitsOption.Eight);
        config.SerialConfig.StopBits.Should().Be(StopBitsOption.One);
        config.SerialConfig.Parity.Should().Be(ParityOption.None);
        config.SerialConfig.FlowControl.Should().Be(FlowControlOption.None);
    }

    [Fact]
    public void Save_WhenValidConfig_CreatesConfigFile()
    {
        // Arrange
        var config = new AppConfig
        {
            SerialConfig = new SerialPortConfig
            {
                PortName = "COM3",
                BaudRate = BaudRate.Baud9600,
                DataBits = DataBitsOption.Seven,
                StopBits = StopBitsOption.Two,
                Parity = ParityOption.Even,
                FlowControl = FlowControlOption.RtsCts
            }
        };

        // Act
        var result = _service.Save(config);

        // Assert
        result.Should().BeTrue();
        File.Exists(_testConfigPath).Should().BeTrue();
    }

    [Fact]
    public void Load_AfterSave_ReturnsCorrectConfig()
    {
        // Arrange
        var originalConfig = new AppConfig
        {
            SerialConfig = new SerialPortConfig
            {
                PortName = "COM5",
                BaudRate = BaudRate.Baud57600,
                DataBits = DataBitsOption.Eight,
                StopBits = StopBitsOption.One,
                Parity = ParityOption.Odd,
                FlowControl = FlowControlOption.XonXoff
            }
        };
        _service.Save(originalConfig);

        // Act
        var loadedConfig = _service.Load();

        // Assert
        loadedConfig.SerialConfig.PortName.Should().Be("COM5");
        loadedConfig.SerialConfig.BaudRate.Should().Be(BaudRate.Baud57600);
        loadedConfig.SerialConfig.DataBits.Should().Be(DataBitsOption.Eight);
        loadedConfig.SerialConfig.StopBits.Should().Be(StopBitsOption.One);
        loadedConfig.SerialConfig.Parity.Should().Be(ParityOption.Odd);
        loadedConfig.SerialConfig.FlowControl.Should().Be(FlowControlOption.XonXoff);
    }

    [Fact]
    public void Save_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var deepPath = Path.Combine(Path.GetTempPath(), $"flexcom_test_{Guid.NewGuid()}", "nested", "config.json");
        var service = new JsonConfigurationService(deepPath);
        var config = new AppConfig();

        // Act
        var result = service.Save(config);

        // Assert
        result.Should().BeTrue();
        File.Exists(deepPath).Should().BeTrue();

        // Cleanup
        var rootDir = Path.GetDirectoryName(Path.GetDirectoryName(deepPath));
        if (rootDir != null && Directory.Exists(rootDir))
        {
            Directory.Delete(rootDir, true);
        }
    }

    [Fact]
    public void Load_WhenFileCorrupted_ReturnsDefaultConfig()
    {
        // Arrange
        var directory = Path.GetDirectoryName(_testConfigPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(_testConfigPath, "{ invalid json }}}");

        // Act
        var config = _service.Load();

        // Assert
        config.Should().NotBeNull();
        config.SerialConfig.BaudRate.Should().Be(BaudRate.Baud115200);
    }

    [Fact]
    public void ConfigFilePath_ReturnsCorrectPath()
    {
        // Assert
        _service.ConfigFilePath.Should().Be(_testConfigPath);
    }

    [Fact]
    public void Save_PreservesAllFlowControlOptions()
    {
        // Test all FlowControl values to ensure proper serialization
        foreach (var flowControl in Enum.GetValues<FlowControlOption>())
        {
            // Arrange
            var config = new AppConfig
            {
                SerialConfig = new SerialPortConfig { FlowControl = flowControl }
            };

            // Act
            _service.Save(config);
            var loaded = _service.Load();

            // Assert
            loaded.SerialConfig.FlowControl.Should().Be(flowControl,
                $"FlowControl {flowControl} should be preserved after save/load");
        }
    }

    [Fact]
    public void Save_PreservesAllBaudRates()
    {
        // Test all BaudRate values
        foreach (var baudRate in Enum.GetValues<BaudRate>())
        {
            // Arrange
            var config = new AppConfig
            {
                SerialConfig = new SerialPortConfig { BaudRate = baudRate }
            };

            // Act
            _service.Save(config);
            var loaded = _service.Load();

            // Assert
            loaded.SerialConfig.BaudRate.Should().Be(baudRate,
                $"BaudRate {baudRate} should be preserved after save/load");
        }
    }
}
