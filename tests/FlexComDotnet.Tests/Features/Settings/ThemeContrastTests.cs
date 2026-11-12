using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace FlexComDotnet.Tests.Features.Settings;

public class ThemeContrastTests
{
    [Fact]
    public void ThemeResources_ShouldResolveBrushesUsedByAllViews()
    {
        var uiRoot = GetUiRoot();
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var themeKeys = new[] { "Dark", "Light" }.Select(theme =>
            XDocument.Load(Path.Combine(uiRoot, "Themes", $"{theme}Theme.xaml"))
                .Descendants().Attributes(xaml + "Key").Select(a => a.Value).ToHashSet()).ToArray();
        var views = Directory.EnumerateFiles(Path.Combine(uiRoot, "Features"), "*.xaml", SearchOption.AllDirectories)
            .Append(Path.Combine(uiRoot, "MainWindow.xaml"));
        foreach (var path in views)
        {
            var source = File.ReadAllText(path);
            var view = XDocument.Parse(source);
            var foreground = view.Root!.Attribute("Foreground")?.Value;
            foreground.Should().Be(view.Root.Attribute("Background")?.Value == "{DynamicResource SidebarBrush}"
                ? "{DynamicResource TextOnSidebarBrush}" : "{DynamicResource TextPrimaryBrush}", path);
            foreach (Match match in Regex.Matches(source, @"\{DynamicResource (\w+Brush)\}"))
                foreach (var keys in themeKeys)
                    keys.Should().Contain(match.Groups[1].Value, path);
        }
    }

    [Fact]
    public void Theme_WhenSwitchingWithExistingControls_ShouldKeepTextReadable()
    {
        FlexComDotnet.Tests.Features.Layout.DockWorkspaceTests.RunSta(VerifyThemes);
    }

    private static void VerifyThemes()
    {
        var uiRoot = GetUiRoot();
        var app = Application.Current;
        var originalResources = app.Resources;
        app.Resources = new ResourceDictionary();
        var failures = new List<string>();
        var window = new Window();
        try
        {
            // Load actual dictionaries without starting hardware/services.
            var appXaml = XDocument.Load(Path.Combine(uiRoot, "App.xaml"));
            foreach (var entry in appXaml.Descendants().Where(e => e.Name.LocalName == "ResourceDictionary" && e.Attribute("Source") != null))
                app.Resources.MergedDictionaries.Add(LoadDictionary(Path.Combine(uiRoot, entry.Attribute("Source")!.Value)));

            // Keep a local BasedOn style alive across theme changes, as real toolbars do.
            var panel = (StackPanel)XamlReader.Parse("""
                <StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                  <Button Content="Toolbar"><Button.Style><Style TargetType="Button" BasedOn="{StaticResource {x:Type Button}}"/></Button.Style></Button>
                  <Expander Header="API Hook" IsExpanded="True"><TextBlock Text="Program path"/></Expander>
                  <RadioButton Content="Response mode"/>
                  <TabControl><TabItem Header="Calls"><TextBlock Text="Details"/></TabItem><TabItem Header="Frames"/></TabControl>
                </StackPanel>
                """);
            // Parse the real API Hook view, removing only code-behind wiring.
            var viewXaml = XDocument.Load(Path.Combine(uiRoot, "Features", "ProcessMonitor", "Views", "ProcessMonitorView.xaml"));
            viewXaml.Root!.Attribute(XName.Get("Class", "http://schemas.microsoft.com/winfx/2006/xaml"))!.Remove();
            foreach (var handler in viewXaml.Descendants().Attributes("Click").ToArray())
                handler.Remove();
            var view = (UserControl)XamlReader.Parse(viewXaml.ToString());
            var host = new Grid();
            host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            host.RowDefinitions.Add(new RowDefinition());
            host.Children.Add(panel);
            Grid.SetRow(view, 1);
            host.Children.Add(view);
            window.Content = host;
            window.SetResourceReference(Control.BackgroundProperty, "BackgroundBrush");
            window.SetResourceReference(Control.ForegroundProperty, "TextPrimaryBrush");
            foreach (var theme in new[] { "Dark", "Light", "Dark" })
            {
                app.Resources.MergedDictionaries[0] = LoadDictionary(Path.Combine(uiRoot, "Themes", $"{theme}Theme.xaml"));
                host.Measure(new Size(1100, 1000));
                host.Arrange(new Rect(0, 0, 1100, 1000));
                host.UpdateLayout();
                foreach (var textKey in new[] { "TextPrimaryBrush", "TextSecondaryBrush", "TextMutedBrush", "PrimaryBrush", "PrimaryLightBrush", "AccentBrush", "SuccessBrush", "WarningBrush", "ErrorBrush", "InfoBrush" })
                    foreach (var surfaceKey in new[] { "BackgroundBrush", "SurfaceBrush", "CardBrush" })
                        Check((Brush)app.Resources[textKey], (Brush)app.Resources[surfaceKey], $"{theme} {textKey}/{surfaceKey}");
                foreach (var surfaceKey in new[] { "PrimaryBrush", "SuccessBrush", "ErrorBrush" })
                    Check((Brush)app.Resources["TextOnPrimaryBrush"], (Brush)app.Resources[surfaceKey], $"{theme} action button/{surfaceKey}");
                foreach (var textKey in new[] { "TextOnSidebarBrush", "SidebarAccentBrush" })
                    Check((Brush)app.Resources[textKey], (Brush)app.Resources["SidebarBrush"], $"{theme} sidebar/{textKey}");
                foreach (var textKey in new[] { "StatusBarForegroundBrush", "StatusBarAccentBrush" })
                    Check((Brush)app.Resources[textKey], (Brush)app.Resources["StatusBarBackgroundBrush"], $"{theme} status bar/{textKey}");

                var button = (Button)panel.Children[0];
                Check(button.Foreground, button.Background, $"{theme} retained toolbar style");
                if (((SolidColorBrush)button.Background).Color != ((SolidColorBrush)app.Resources["SurfaceBrush"]).Color)
                    failures.Add($"{theme} toolbar retained the previous theme's background");
                var expander = (Expander)panel.Children[1];
                Check(expander.Foreground, window.Background, $"{theme} Expander header");
                Check(((TextBlock)expander.Content).Foreground, window.Background, $"{theme} text inside Expander");
                Check(((RadioButton)panel.Children[2]).Foreground, window.Background, $"{theme} RadioButton");
                var tabs = (TabControl)panel.Children[3];
                foreach (TabItem tab in tabs.Items)
                {
                    tabs.SelectedItem = tab;
                    host.UpdateLayout();
                    Check(tab.Foreground, tab.Background, $"{theme} selected TabItem");
                }
                var actualTabs = VisualChildren(view).OfType<TabControl>().Single();
                foreach (TabItem tab in actualTabs.Items)
                {
                    actualTabs.SelectedItem = tab;
                    host.UpdateLayout();
                    var grid = VisualChildren(view).OfType<DataGrid>().Single();
                    grid.ItemsSource = new[] { new { DataText = "01 03 00 00 00 0A", Record = new { Api = "ReadFile", PortName = "COM3" } } };
                    grid.SelectedIndex = 0;
                    host.UpdateLayout();
                    var cells = VisualChildren(grid).OfType<DataGridCell>().ToArray();
                    cells.Should().NotBeEmpty("the real API Hook table must generate cells");
                    foreach (var cell in cells)
                        Check(cell.Foreground, cell.Background, $"{theme} API Hook selected cell");
                    foreach (var header in VisualChildren(grid).OfType<DataGridColumnHeader>())
                        Check(header.Foreground, header.Background, $"{theme} API Hook table header");
                    foreach (var text in VisualChildren(view).OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text)))
                        Check(text.Foreground, EffectiveBackground(text, window.Background), $"{theme} API Hook {text.Text}");
                    SavePreview(view, $"{theme}-{actualTabs.SelectedIndex}.png");
                }
            }
            string.Join(Environment.NewLine, failures).Should().BeEmpty("visible text and existing controls must stay readable in both themes");
        }
        finally
        {
            window.Close();
            app.Resources = originalResources;
        }

        void Check(Brush foreground, Brush background, string label)
        {
            if (foreground is not SolidColorBrush fg || background is not SolidColorBrush bg)
            {
                failures.Add($"{label}: missing solid foreground/background");
                return;
            }
            var a = Luminance(fg.Color);
            var b = Luminance(bg.Color);
            var ratio = (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
            if (ratio < 4.5)
                failures.Add($"{label}: {fg.Color} on {bg.Color}, contrast {ratio:F2}");
        }
    }

    private static ResourceDictionary LoadDictionary(string path) => new() { Source = new Uri(path, UriKind.Absolute) };

    private static string GetUiRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "src", "FlexComDotnet", "App.xaml")))
            root = root.Parent;
        root.Should().NotBeNull();
        return Path.Combine(root!.FullName, "src", "FlexComDotnet");
    }

    private static void SavePreview(FrameworkElement view, string filename)
    {
        var directory = Environment.GetEnvironmentVariable("FLEX_THEME_PREVIEW_DIRECTORY");
        if (string.IsNullOrEmpty(directory))
            return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
            context.DrawRectangle(new VisualBrush(view), null, new Rect(0, 0, view.ActualWidth, view.ActualHeight));
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, filename));
        encoder.Save(stream);
    }

    private static IEnumerable<DependencyObject> VisualChildren(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in VisualChildren(child))
                yield return descendant;
        }
    }

    private static Brush EffectiveBackground(DependencyObject element, Brush fallback)
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent != null; parent = VisualTreeHelper.GetParent(parent))
        {
            var background = parent switch
            {
                Border border => border.Background,
                Panel panel => panel.Background,
                Control control => control.Background,
                _ => null
            };
            if (background is SolidColorBrush { Color.A: 255 })
                return background;
        }
        return fallback;
    }

    private static double Luminance(Color color)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return Linear(color.R) * 0.2126 + Linear(color.G) * 0.7152 + Linear(color.B) * 0.0722;
    }
}
