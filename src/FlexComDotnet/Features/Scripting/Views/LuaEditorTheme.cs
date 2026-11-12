using System.Windows;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace FlexComDotnet.Features.Scripting.Views;

/// <summary>Updates AvalonEdit's syntax palette through the same resources as the docked page.</summary>
public static class LuaEditorTheme
{
    private static readonly DependencyProperty PaletteProperty = DependencyProperty.RegisterAttached(
        "Palette", typeof(Brush), typeof(LuaEditorTheme), new PropertyMetadata(null, OnPaletteChanged));

    public static void Initialize(TextEditor editor)
    {
        using var stream = typeof(LuaEditorTheme).Assembly.GetManifestResourceStream(
            "FlexComDotnet.Features.Scripting.Resources.LuaSyntax.xshd");
        if (stream == null) return;
        using var reader = XmlReader.Create(stream);
        editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        editor.SetResourceReference(PaletteProperty, "BackgroundBrush");
        ApplyPalette(editor);
    }

    private static void OnPaletteChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is TextEditor editor) ApplyPalette(editor);
    }

    private static void ApplyPalette(TextEditor editor)
    {
        if (editor.SyntaxHighlighting == null) return;
        foreach (var token in editor.SyntaxHighlighting.NamedHighlightingColors)
            if (editor.TryFindResource("Lua" + token.Name + "Color") is Color color)
                token.Foreground = new SimpleHighlightingBrush(color);
        editor.TextArea.TextView.Redraw();
    }
}
