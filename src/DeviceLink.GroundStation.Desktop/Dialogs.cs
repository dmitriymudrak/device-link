using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Threading.Tasks;

namespace DeviceLink.GroundStation.Desktop;

/// <summary>Simple modal dialogs built in code without XAML.</summary>
internal static class Dialogs
{
    /// <summary>Shows a message. Without an owner the window opens standalone (e.g. before MainWindow exists).</summary>
    public static Task ShowMessageAsync(string title, string text, Window? owner = null)
    {
        var ok = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var w = new Window
        {
            Title = title,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children = { new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, ok }
            }
        };
        ok.Click += (_, _) => w.Close();

        if (owner is not null)
            return w.ShowDialog(owner);

        var closed = new TaskCompletionSource();
        w.Closed += (_, _) => closed.TrySetResult();
        w.Show();
        return closed.Task;
    }
}
