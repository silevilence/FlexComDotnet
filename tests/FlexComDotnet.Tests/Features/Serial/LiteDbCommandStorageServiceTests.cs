using FlexComDotnet.Core.Features.Serial.Models;
using FlexComDotnet.Core.Features.Serial.Services;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Serial;

/// <summary>
/// LiteDbCommandStorageService 测试
/// </summary>
public class LiteDbCommandStorageServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly LiteDbCommandStorageService _service;

    public LiteDbCommandStorageServiceTests()
    {
        // 使用临时文件作为测试数据库
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_commands_{Guid.NewGuid()}.db");
        _service = new LiteDbCommandStorageService(_testDbPath);
    }

    public void Dispose()
    {
        _service.Dispose();
        // 清理测试数据库文件
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_ShouldCreateDatabaseFile()
    {
        // Assert
        File.Exists(_testDbPath).Should().BeTrue();
        _service.DatabasePath.Should().Be(_testDbPath);
    }

    [Fact]
    public void GetAll_EmptyDatabase_ShouldReturnEmptyList()
    {
        // Act
        var result = _service.GetAll();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Add_ValidItem_ShouldReturnPositiveId()
    {
        // Arrange
        var item = new CommandItem
        {
            Name = "Test Command",
            Content = "AA BB CC"
        };

        // Act
        var id = _service.Add(item);

        // Assert
        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Add_NullItem_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.Add(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Add_ShouldAutoIncrementSortOrder()
    {
        // Arrange
        var item1 = new CommandItem { Name = "Command 1" };
        var item2 = new CommandItem { Name = "Command 2" };
        var item3 = new CommandItem { Name = "Command 3" };

        // Act
        _service.Add(item1);
        _service.Add(item2);
        _service.Add(item3);
        var items = _service.GetAll();

        // Assert
        items.Should().HaveCount(3);
        items[0].SortOrder.Should().Be(1);
        items[1].SortOrder.Should().Be(2);
        items[2].SortOrder.Should().Be(3);
    }

    [Fact]
    public void GetById_ExistingItem_ShouldReturnItem()
    {
        // Arrange
        var item = new CommandItem { Name = "Test", Content = "01 02 03" };
        var id = _service.Add(item);

        // Act
        var result = _service.GetById(id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
        result.Content.Should().Be("01 02 03");
    }

    [Fact]
    public void GetById_NonExistingItem_ShouldReturnNull()
    {
        // Act
        var result = _service.GetById(99999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Update_ExistingItem_ShouldReturnTrue()
    {
        // Arrange
        var item = new CommandItem { Name = "Original" };
        var id = _service.Add(item);
        var toUpdate = _service.GetById(id)!;
        toUpdate.Name = "Updated";
        toUpdate.Content = "New Content";

        // Act
        var result = _service.Update(toUpdate);
        var updated = _service.GetById(id);

        // Assert
        result.Should().BeTrue();
        updated!.Name.Should().Be("Updated");
        updated.Content.Should().Be("New Content");
    }

    [Fact]
    public void Update_NullItem_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.Update(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Delete_ExistingItem_ShouldReturnTrue()
    {
        // Arrange
        var item = new CommandItem { Name = "To Delete" };
        var id = _service.Add(item);

        // Act
        var result = _service.Delete(id);
        var deleted = _service.GetById(id);

        // Assert
        result.Should().BeTrue();
        deleted.Should().BeNull();
    }

    [Fact]
    public void Delete_NonExistingItem_ShouldReturnFalse()
    {
        // Act
        var result = _service.Delete(99999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetAll_ShouldReturnItemsOrderedBySortOrder()
    {
        // Arrange
        var item1 = new CommandItem { Name = "Third", SortOrder = 3 };
        var item2 = new CommandItem { Name = "First", SortOrder = 1 };
        var item3 = new CommandItem { Name = "Second", SortOrder = 2 };

        _service.Add(item1);
        _service.Add(item2);
        _service.Add(item3);

        // Act
        var result = _service.GetAll();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("First");
        result[1].Name.Should().Be("Second");
        result[2].Name.Should().Be("Third");
    }

    [Fact]
    public void UpdateSortOrder_ValidItems_ShouldReturnTrue()
    {
        // Arrange
        var item1 = new CommandItem { Name = "A" };
        var item2 = new CommandItem { Name = "B" };
        var item3 = new CommandItem { Name = "C" };

        var id1 = _service.Add(item1);
        var id2 = _service.Add(item2);
        var id3 = _service.Add(item3);

        var updates = new[]
        {
            new CommandItem { Id = id1, SortOrder = 3 },
            new CommandItem { Id = id2, SortOrder = 1 },
            new CommandItem { Id = id3, SortOrder = 2 }
        };

        // Act
        var result = _service.UpdateSortOrder(updates);
        var items = _service.GetAll();

        // Assert
        result.Should().BeTrue();
        items[0].Name.Should().Be("B"); // SortOrder = 1
        items[1].Name.Should().Be("C"); // SortOrder = 2
        items[2].Name.Should().Be("A"); // SortOrder = 3
    }

    [Fact]
    public void UpdateSortOrder_NullItems_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.UpdateSortOrder(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
