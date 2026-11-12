using DeviceLink.GroundStation.Desktop.Auth;
using DeviceLink.GroundStation.Desktop.Options;
using DeviceLink.GroundStation.Desktop.Services;
using Duende.IdentityModel.OidcClient.Browser;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;

namespace DeviceLink.GroundStation.Desktop;

/// <summary>DI registration for the desktop ground station services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers options, authentication, StationService/ApiForwarder and windows.</summary>
    public static IServiceCollection AddGroundStationDesktop(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>().BindConfiguration("Auth")
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ApiOptions>().BindConfiguration("Api")
            .ValidateDataAnnotations().ValidateOnStart();

        // Auth
        services.AddSingleton<TokenStore>();
        services.AddSingleton<IBrowser, LoopbackBrowser>();
        services.AddSingleton<AuthService>();

        // Station
        services.AddSingleton(sp =>
        {
            var api = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
            var auth = sp.GetRequiredService<AuthService>();
            return new ApiForwarder(api.Address, api.StationName, auth.GetAccessTokenAsync,
                sp.GetRequiredService<ILogger<ApiForwarder>>());
        });
        services.AddSingleton<StationService>();

        // UI
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
        services.AddSingleton<Func<LoginWindow>>(sp => () => sp.GetRequiredService<LoginWindow>());

        return services;
    }
}
