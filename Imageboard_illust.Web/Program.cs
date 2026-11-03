using System.Security.Claims;
using Imageboard_illust.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSingleton<AppDatabase>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// OAuth providers are only wired up once their credentials are configured (via dotnet user-secrets
// or environment variables) so the app still runs fine with zero providers set up.
bool HasCreds(string clientIdKey, string clientSecretKey) =>
    !string.IsNullOrEmpty(builder.Configuration[clientIdKey]) &&
    !string.IsNullOrEmpty(builder.Configuration[clientSecretKey]);

bool googleEnabled = HasCreds("Authentication:Google:ClientId", "Authentication:Google:ClientSecret");
bool microsoftEnabled = HasCreds("Authentication:Microsoft:ClientId", "Authentication:Microsoft:ClientSecret");
bool facebookEnabled = HasCreds("Authentication:Facebook:AppId", "Authentication:Facebook:AppSecret");
bool xEnabled = HasCreds("Authentication:X:ClientId", "Authentication:X:ClientSecret");

builder.Services.AddSingleton(new OAuthProviderStatus(googleEnabled, microsoftEnabled, facebookEnabled, xEnabled));

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie(options =>
{
    // This cookie only carries the external login result to our callback below; the app's own
    // notion of "logged in" stays Session-based, matching the existing email/password flow.
    options.Cookie.Name = "ImageboardExternalLogin";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
});

if (googleEnabled)
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.CallbackPath = "/signin-google";
    });
}

if (microsoftEnabled)
{
    authBuilder.AddMicrosoftAccount(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"]!;
        options.CallbackPath = "/signin-microsoft";
    });
}

if (facebookEnabled)
{
    authBuilder.AddFacebook(options =>
    {
        options.AppId = builder.Configuration["Authentication:Facebook:AppId"]!;
        options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"]!;
        options.CallbackPath = "/signin-facebook";
    });
}

if (xEnabled)
{
    // X's OAuth 2.0 API has no official ASP.NET Core package and generally does not return an
    // email address even with the "users.read" scope, so this uses the generic OAuth handler and
    // the callback below falls back to a synthetic placeholder email for account matching.
    authBuilder.AddOAuth("X", "X", options =>
    {
        options.ClientId = builder.Configuration["Authentication:X:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:X:ClientSecret"]!;
        options.CallbackPath = "/signin-x";
        options.AuthorizationEndpoint = "https://twitter.com/i/oauth2/authorize";
        options.TokenEndpoint = "https://api.twitter.com/2/oauth2/token";
        options.UserInformationEndpoint = "https://api.twitter.com/2/users/me";
        options.UsePkce = true;
        options.Scope.Add("users.read");
        options.Scope.Add("tweet.read");
        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        options.ClaimActions.MapJsonKey("urn:x:username", "username");
        options.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
                using var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();
                using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
                context.RunClaimActions(json.RootElement.GetProperty("data"));
            }
        };
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/boardimages/{boardId:int}/{fileName}", (int boardId, string fileName, HttpContext ctx, AppDatabase db) =>
{
    var userId = ctx.Session.GetInt32("UserId");
    if (userId is null || !db.CanUserAccessBoard(boardId, userId.Value))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var safeFileName = Path.GetFileName(fileName);
    var path = Path.Combine(db.DataDir, "boards", boardId.ToString(), "images", safeFileName);
    if (!File.Exists(path)) return Results.NotFound();

    var contentType = Path.GetExtension(safeFileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };
    return Results.File(path, contentType);
});

app.MapGet("/ExternalLogin/Challenge", (string provider) =>
{
    var properties = new AuthenticationProperties
    {
        RedirectUri = "/ExternalLogin/Callback?provider=" + Uri.EscapeDataString(provider),
    };
    return Results.Challenge(properties, [provider]);
});

app.MapGet("/ExternalLogin/Callback", async (string provider, HttpContext ctx, AppDatabase db) =>
{
    var result = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    if (!result.Succeeded || result.Principal is null)
        return Results.Redirect("/Login?error=external");

    var email = result.Principal.FindFirstValue(ClaimTypes.Email);
    if (string.IsNullOrEmpty(email) && provider == "X")
    {
        // X does not expose a real email via OAuth; fall back to a synthetic placeholder keyed
        // on their stable user id so the same X account always maps to the same local account.
        var xUserId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(xUserId))
            email = $"x-{xUserId}@x.oauth.local";
    }

    if (string.IsNullOrEmpty(email))
        return Results.Redirect("/Login?error=noemail");

    var user = db.GetOrCreateOAuthUser(email);
    ctx.Session.SetInt32("UserId", user.Id);
    ctx.Session.SetString("UserEmail", user.Email);

    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/Index");
});

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public record OAuthProviderStatus(bool Google, bool Microsoft, bool Facebook, bool X);
