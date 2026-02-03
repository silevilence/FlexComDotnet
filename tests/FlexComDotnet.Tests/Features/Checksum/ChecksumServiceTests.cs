using FluentAssertions;
using FlexComDotnet.Core.Features.Checksum.Models;
using FlexComDotnet.Core.Features.Checksum.Services;

namespace FlexComDotnet.Tests.Features.Checksum;

/// <summary>
/// 校验和服务测试
/// </summary>
public class ChecksumServiceTests
{
    private readonly ChecksumService _service;

    public ChecksumServiceTests()
    {
        _service = new ChecksumService();
    }

    #region GetAllAlgorithms 测试

    [Fact]
    public void GetAllAlgorithms_ShouldReturnAllAlgorithms()
    {
        var algorithms = _service.GetAllAlgorithms();

        algorithms.Should().HaveCountGreaterThanOrEqualTo(11);
        algorithms.Select(a => a.Type).Should().Contain(ChecksumAlgorithmType.Sum8);
        algorithms.Select(a => a.Type).Should().Contain(ChecksumAlgorithmType.Sum16);
        algorithms.Select(a => a.Type).Should().Contain(ChecksumAlgorithmType.Xor);
        algorithms.Select(a => a.Type).Should().Contain(ChecksumAlgorithmType.Crc8);
        algorithms.Select(a => a.Type).Should().Contain(ChecksumAlgorithmType.Crc16Modbus);
        algorithms.Select(a => a.Type).Should().Contain(ChecksumAlgorithmType.Crc16CcittFalse);
        algorithms.Select(a => a.Type).Should().Contain(ChecksumAlgorithmType.Crc16Xmodem);
        algorithms.Select(a => a.Type).Should().Contain(ChecksumAlgorithmType.Crc32);
        algorithms.Select(a => a.Type).Should().Contain(ChecksumAlgorithmType.Md5);
        algorithms.Select(a => a.Type).Should().Contain(ChecksumAlgorithmType.Sha1);
        algorithms.Select(a => a.Type).Should().Contain(ChecksumAlgorithmType.Sha256);
    }

    [Fact]
    public void GetAllAlgorithms_EachAlgorithmShouldHaveRequiredProperties()
    {
        var algorithms = _service.GetAllAlgorithms();

        foreach (var algorithm in algorithms)
        {
            algorithm.DisplayName.Should().NotBeNullOrEmpty();
            algorithm.Description.Should().NotBeNullOrEmpty();
            algorithm.ResultLength.Should().BeGreaterThan(0);
        }
    }

    #endregion

    #region GetAlgorithm 测试

    [Theory]
    [InlineData(ChecksumAlgorithmType.Sum8)]
    [InlineData(ChecksumAlgorithmType.Sum16)]
    [InlineData(ChecksumAlgorithmType.Xor)]
    [InlineData(ChecksumAlgorithmType.Crc8)]
    [InlineData(ChecksumAlgorithmType.Crc16Modbus)]
    [InlineData(ChecksumAlgorithmType.Crc32)]
    [InlineData(ChecksumAlgorithmType.Md5)]
    [InlineData(ChecksumAlgorithmType.Sha1)]
    [InlineData(ChecksumAlgorithmType.Sha256)]
    public void GetAlgorithm_WithValidType_ShouldReturnAlgorithm(ChecksumAlgorithmType type)
    {
        var algorithm = _service.GetAlgorithm(type);

        algorithm.Should().NotBeNull();
        algorithm.Type.Should().Be(type);
    }

    [Fact]
    public void GetAlgorithm_WithInvalidType_ShouldThrowException()
    {
        var invalidType = (ChecksumAlgorithmType)999;

        var action = () => _service.GetAlgorithm(invalidType);

        action.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Calculate 测试

    [Fact]
    public void Calculate_ShouldDelegateToAlgorithm()
    {
        var data = new byte[] { 0x01, 0x02, 0x03 };

        var result = _service.Calculate(ChecksumAlgorithmType.Sum8, data);

        // 0x01 + 0x02 + 0x03 = 0x06
        result.Should().Equal([0x06]);
    }

    [Fact]
    public void CalculateAsHexString_ShouldDelegateToAlgorithm()
    {
        var data = new byte[] { 0x01, 0x02, 0x03 };

        var result = _service.CalculateAsHexString(ChecksumAlgorithmType.Sum8, data);

        result.Should().Be("06");
    }

    #endregion
}
