using Avalonia.Controls;
using Avalonia.Interactivity;
using DeviceLink.GroundStation.Desktop.Auth;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceLink.GroundStation.Desktop;

/// <summary>Login wait window: starts OIDC login in the browser and lets the user retry or cancel.</summary>
public partial class LoginWindow : Window
{
    private readonly AuthService? _auth;
    private readonly ILogger<LoginWindow>? _logger;
    private CancellationTokenSource _cts = new();
    private Task _login = Task.CompletedTask;

    /// <summary>Whether login completed successfully.</summary>
    public bool Success { get; private set; }

    // For the Avalonia designer.
    public LoginWindow() => InitializeComponent();

    /// <summary>Creates the window, which starts login automatically once opened.</summary>
    public LoginWindow(AuthService auth, ILogger<LoginWindow> logger) : this()
    {
        _auth = auth;
        _logger = logger;
        Opened += (_, _) => _login = RunLoginAsync();
    }

    /// <summary>Shows the window and waits until it closes; true if login succeeded.</summary>
    public async Task<bool> ShowAndWaitAsync()
    {
        var closed = new TaskCompletionSource();
        Closed += (_, _) => closed.TrySetResult();
        Show();
        await closed.Task;
        return Success;
    }

    /// <summary>Runs one login attempt with a fresh cancellation token; closes the window on success.</summary>
    private async Task RunLoginAsync()
    {
        _cts = new CancellationTokenSource();
        try
        {
            if (await _auth!.LoginAsync(_cts.Token))
            {
                Success = true;
                Close();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Login failed");
        }
    }

    /// <summary>Cancels the current attempt, waits for it to finish and starts login again.</summary>
    private async void Retry_Click(object? s, RoutedEventArgs e)
    {
        _logger?.LogInformation("Login retry requested");
        _cts.Cancel();
        await _login;          // the previous HttpListener must release the port
        _login = RunLoginAsync();
    }

    /// <summary>Cancels login and closes the window.</summary>
    private void Cancel_Click(object? s, RoutedEventArgs e)
    {
        _logger?.LogInformation("Login cancelled by user");
        _cts.Cancel();
        Close();
    }

    /// <summary>Cancels a pending login when the window closes.</summary>
    protected override void OnClosed(EventArgs e)
    {
        _cts.Cancel();
        base.OnClosed(e);
    }
}
