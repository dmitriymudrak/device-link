using Avalonia.Controls;
using Avalonia.Interactivity;
using DeviceLink.GroundStation.Desktop.Auth;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceLink.GroundStation.Desktop;

public partial class LoginWindow : Window
{
    CancellationTokenSource _cts = new();
    public bool Success { get; private set; }
    private readonly AuthService _auth;
    public LoginWindow() => InitializeComponent();
    public LoginWindow(AuthService auth) : this()
    {
        _auth = auth;
        Opened += async (_, _) => await RunLoginAsync();
    }

    async Task RunLoginAsync()
    {
        _cts = new CancellationTokenSource();
        if (await App.Auth.LoginAsync(_cts.Token)) { Success = true; Close(); }
    }

    async void Retry_Click(object? s, RoutedEventArgs e) { _cts.Cancel(); await RunLoginAsync(); }
    void Cancel_Click(object? s, RoutedEventArgs e) { _cts.Cancel(); Close(); }

    protected override void OnClosed(EventArgs e) { _cts.Cancel(); base.OnClosed(e); }
}