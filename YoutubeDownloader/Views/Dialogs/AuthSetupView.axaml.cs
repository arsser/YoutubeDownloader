using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using YoutubeDownloader.Framework;
using YoutubeDownloader.ViewModels.Dialogs;

namespace YoutubeDownloader.Views.Dialogs;

public partial class AuthSetupView : UserControl<AuthSetupViewModel>
{
    private static readonly Uri HomePageUri = new("https://www.youtube.com");
    private static readonly Uri LoginPageUri = new(
        $"https://accounts.google.com/ServiceLogin?continue={Uri.EscapeDataString(HomePageUri.AbsoluteUri)}"
    );

    private NativeWebDialog? _loginDialog;

    public AuthSetupView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs args)
    {
        if (!DataContext.IsAuthenticated)
            ShowLoginDialog();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs args) => CloseLoginDialog();

    private async void LogOutButton_OnClick(object sender, RoutedEventArgs args)
    {
        DataContext.Cookies = null;
        await ClearCookiesAsync();
        ShowLoginDialog();
    }

    private void ShowLoginDialog()
    {
        CloseLoginDialog();

        var dialog = new NativeWebDialog
        {
            Title = "YoutubeDownloader - " + DataContext.LocalizationManager.AuthenticationTitle,
            CanUserResize = true,
            Source = LoginPageUri,
        };

        dialog.EnvironmentRequested += OnEnvironmentRequested;
        dialog.NavigationStarted += OnNavigationStarted;
        dialog.NavigationCompleted += OnNavigationCompleted;
        dialog.Resize(960, 720);

        _loginDialog = dialog;

        if (TopLevel.GetTopLevel(this) is { } owner)
            dialog.Show(owner);
        else
            dialog.Show();
    }

    private void CloseLoginDialog()
    {
        if (_loginDialog is null)
            return;

        var dialog = _loginDialog;
        _loginDialog = null;

        dialog.EnvironmentRequested -= OnEnvironmentRequested;
        dialog.NavigationStarted -= OnNavigationStarted;
        dialog.NavigationCompleted -= OnNavigationCompleted;
        dialog.Close();
        dialog.Dispose();
    }

    private static void OnEnvironmentRequested(
        object? sender,
        WebViewEnvironmentRequestedEventArgs args
    )
    {
        args.EnableDevTools = false;

        if (args is WindowsWebView2EnvironmentRequestedEventArgs webView2)
            webView2.IsInPrivateModeEnabled = true;
    }

    private async void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs args)
    {
        var url = args.Request;
        if (url is null || _loginDialog is null)
            return;

        if (
            string.Equals(
                url.AbsoluteUri,
                LoginPageUri.AbsoluteUri,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            await ClearCookiesAsync();
        }
    }

    private async void OnNavigationCompleted(
        object? sender,
        WebViewNavigationCompletedEventArgs args
    )
    {
        var url = args.Request;
        if (url is null || _loginDialog is null || DataContext.IsAuthenticated)
            return;

        if (
            !string.Equals(url.Scheme, HomePageUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(url.Host, HomePageUri.Host, StringComparison.OrdinalIgnoreCase)
        )
            return;

        await Task.Delay(500);

        var cookieManager = _loginDialog?.TryGetCookieManager();
        if (cookieManager is null)
            return;

        var cookies = await cookieManager.GetCookiesAsync();
        if (cookies.Count == 0)
            return;

        DataContext.Cookies = cookies.ToArray();
        if (DataContext.IsAuthenticated)
            CloseLoginDialog();
    }

    private async Task ClearCookiesAsync()
    {
        var cookieManager = _loginDialog?.TryGetCookieManager();
        if (cookieManager is null)
            return;

        foreach (var cookie in await cookieManager.GetCookiesAsync())
            cookieManager.DeleteCookie(cookie);
    }
}
