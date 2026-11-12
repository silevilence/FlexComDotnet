using System.Windows;
using System.Windows.Media;
using FluentAssertions;
using ICSharpCode.AvalonEdit;

namespace FlexComDotnet.Tests.Features.Layout;

public class LuaEditorThemeTests
{
    [Fact]
    public void SyntaxColors_ShouldFollowThemeResourcesWithoutReplacingText() => DockWorkspaceTests.RunSta(() =>
    {
        var editor = new TextEditor { Text = "print('draft')" };
        var light = new ResourceDictionary { Source = new Uri("/FlexComDotnet;component/Themes/LightTheme.xaml", UriKind.Relative) };
        var dark = new ResourceDictionary { Source = new Uri("/FlexComDotnet;component/Themes/DarkTheme.xaml", UriKind.Relative) };
        editor.Resources.MergedDictionaries.Add(dark);
        FlexComDotnet.Features.Scripting.Views.LuaEditorTheme.Initialize(editor);
        var before = editor.SyntaxHighlighting.GetNamedColor("String").Foreground.GetColor(null!);
        editor.Resources.MergedDictionaries.Clear();
        editor.Resources.MergedDictionaries.Add(light);
        var after = editor.SyntaxHighlighting.GetNamedColor("String").Foreground.GetColor(null!);
        after.Should().NotBe(before);
        after.Should().Be((Color)light["LuaStringColor"]);
        editor.Text.Should().Be("print('draft')");
    });
}
