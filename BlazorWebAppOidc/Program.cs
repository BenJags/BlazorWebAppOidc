using System.Security.Claims;
using BlazorWebAppOidc;
using BlazorWebAppOidc.Client.Weather;
using BlazorWebAppOidc.Components;
using BlazorWebAppOidc.Weather;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

const string MS_OIDC_SCHEME = "MicrosoftOidc";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthentication(MS_OIDC_SCHEME)
    .AddOpenIdConnect(MS_OIDC_SCHEME, oidcOptions =>
    {
        oidcOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

        oidcOptions.Authority = "https://host.docker.internal/keycloak/realms/Autostore/";

        oidcOptions.ClientId = "WMSServiceCalendar";

        oidcOptions.ResponseType = OpenIdConnectResponseType.Code;

        oidcOptions.MapInboundClaims = false;
        oidcOptions.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = "role"
        };

        oidcOptions.UsePkce = true;
        oidcOptions.GetClaimsFromUserInfoEndpoint = true;
        oidcOptions.ClaimActions.MapJsonKey("role", "role");

        oidcOptions.Events.OnUserInformationReceived = ctx =>
        {
            Console.WriteLine();
            Console.WriteLine("Claims from the ID token");
            foreach (var claim in ctx.Principal.Claims)
            {
                Console.WriteLine($"{claim.Type} - {claim.Value}");
            }
            Console.WriteLine();
            Console.WriteLine("Claims from the UserInfo endpoint");
            foreach (var property in ctx.User.RootElement.EnumerateObject())
            {
                Console.WriteLine($"{property.Name} - {property.Value}");
            }

            return Task.CompletedTask;
        };
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Events.OnSigningIn = ctx =>
        {
            Console.WriteLine();
            Console.WriteLine("Claims received by the Cookie handler");
            foreach (var claim in ctx.Principal.Claims)
            {
                Console.WriteLine($"{claim.Type} - {claim.Value}");
            }
            Console.WriteLine();

            return Task.CompletedTask;
        };
    });

builder.Services.ConfigureCookieOidcRefresh(CookieAuthenticationDefaults.AuthenticationScheme, MS_OIDC_SCHEME);

builder.Services.AddAuthorization();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();

builder.Services.AddScoped<IWeatherForecaster, ServerWeatherForecaster>();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/weather-forecast", ([FromServices] IWeatherForecaster WeatherForecaster, ClaimsPrincipal user) =>
{
    Console.WriteLine();
    Console.WriteLine("Claims received by the weather minimal api");
    foreach (var claim in user.Claims)
    {
        Console.WriteLine($"{claim.Type} - {claim.Value}");
    }

    return WeatherForecaster.GetWeatherForecastAsync();
}).RequireAuthorization();

app.MapGet("/claims", (ClaimsPrincipal user) => user.Claims.Select(c => new { c.Type, c.Value }));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(BlazorWebAppOidc.Client._Imports).Assembly);

app.MapGroup("/authentication").MapLoginAndLogout();

app.Run();
