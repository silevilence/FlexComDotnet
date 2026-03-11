using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlexComDotnet.Core.Features.EmojiSupport.Models;
using FlexComDotnet.Core.Features.EmojiSupport.Services;
using FlexComDotnet.Features.EmojiSupport.Controls;

namespace FlexComDotnet.Features.EmojiSupport.Behaviors;

/// <summary>
/// Emoji 补全附加行为 - 为 TextBox 添加 Emoji 短码补全功能。
/// 用法: &lt;TextBox emoji:EmojiCompletionBehavior.IsEnabled="True" /&gt;
/// 当用户输入 : 后跟有效字符时弹出候选列表。
/// </summary>
public static class EmojiCompletionBehavior
{
    private static readonly IEmojiService s_emojiService = new EmojiService();

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(EmojiCompletionBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static readonly DependencyProperty PopupProperty =
        DependencyProperty.RegisterAttached(
            "Popup",
            typeof(EmojiCompletionPopup),
            typeof(EmojiCompletionBehavior));

    private static readonly DependencyProperty ColonStartProperty =
        DependencyProperty.RegisterAttached(
            "ColonStart",
            typeof(int),
            typeof(EmojiCompletionBehavior),
            new PropertyMetadata(-1));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox) return;

        if ((bool)e.NewValue)
        {
            textBox.TextChanged += TextBox_TextChanged;
            textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            textBox.LostFocus += TextBox_LostFocus;
        }
        else
        {
            textBox.TextChanged -= TextBox_TextChanged;
            textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;
            textBox.LostFocus -= TextBox_LostFocus;
            CleanupPopup(textBox);
        }
    }

    private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        var text = textBox.Text;
        var caretIndex = textBox.CaretIndex;

        if (caretIndex <= 0 || string.IsNullOrEmpty(text))
        {
            DismissPopup(textBox);
            return;
        }

        var colonIndex = FindColonStart(text, caretIndex);
        if (colonIndex < 0)
        {
            DismissPopup(textBox);
            return;
        }

        var queryStart = colonIndex + 1;
        var queryLength = caretIndex - queryStart;
        if (queryLength <= 0)
        {
            DismissPopup(textBox);
            return;
        }

        var query = text.Substring(queryStart, queryLength);
        if (query.Any(c => char.IsWhiteSpace(c) || c == ':'))
        {
            DismissPopup(textBox);
            return;
        }

        var results = s_emojiService.Search(query, 8);
        if (results.Count == 0)
        {
            DismissPopup(textBox);
            return;
        }

        textBox.SetValue(ColonStartProperty, colonIndex);

        var popup = GetOrCreatePopup(textBox);
        popup.UpdateItems(results);

        if (!popup.IsOpen)
        {
            var rect = textBox.GetRectFromCharacterIndex(colonIndex);
            popup.PlacementTarget = textBox;
            popup.HorizontalOffset = rect.Left;
            popup.VerticalOffset = rect.Bottom + 2;
            popup.IsOpen = true;
        }
    }

    private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        var popup = textBox.GetValue(PopupProperty) as EmojiCompletionPopup;
        if (popup == null || !popup.IsOpen) return;

        switch (e.Key)
        {
            case Key.Up:
                popup.MoveUp();
                e.Handled = true;
                break;
            case Key.Down:
                popup.MoveDown();
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Tab:
                if (popup.ConfirmSelection())
                    e.Handled = true;
                break;
            case Key.Escape:
                DismissPopup(textBox);
                e.Handled = true;
                break;
            case Key.Space:
                DismissPopup(textBox);
                break;
        }
    }

    private static void TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
            DismissPopup(textBox);
    }

    private static int FindColonStart(string text, int caretIndex)
    {
        for (int i = caretIndex - 1; i >= 0; i--)
        {
            if (text[i] == ':')
                return i;
            if (char.IsWhiteSpace(text[i]) || (char.IsPunctuation(text[i]) && text[i] != '_'))
                return -1;
        }
        return -1;
    }

    private static EmojiCompletionPopup GetOrCreatePopup(TextBox textBox)
    {
        var popup = textBox.GetValue(PopupProperty) as EmojiCompletionPopup;
        if (popup == null)
        {
            popup = new EmojiCompletionPopup();
            popup.EmojiSelected += (_, entry) => OnEmojiSelected(textBox, entry);
            textBox.SetValue(PopupProperty, popup);
        }
        return popup;
    }

    private static void OnEmojiSelected(TextBox textBox, EmojiEntry entry)
    {
        var colonStart = (int)textBox.GetValue(ColonStartProperty);
        if (colonStart < 0) return;

        var caretIndex = textBox.CaretIndex;
        var text = textBox.Text;
        var newText = text[..colonStart] + entry.Emoji + text[caretIndex..];

        textBox.Text = newText;
        textBox.CaretIndex = colonStart + entry.Emoji.Length;
        textBox.SetValue(ColonStartProperty, -1);
    }

    private static void DismissPopup(TextBox textBox)
    {
        if (textBox.GetValue(PopupProperty) is EmojiCompletionPopup popup)
            popup.IsOpen = false;
        textBox.SetValue(ColonStartProperty, -1);
    }

    private static void CleanupPopup(TextBox textBox)
    {
        if (textBox.GetValue(PopupProperty) is EmojiCompletionPopup popup)
            popup.IsOpen = false;
        textBox.ClearValue(PopupProperty);
    }
}
