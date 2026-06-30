using BoardGameFanatics.Components;
using BoardGameFanatics.Data;
using BoardGameFanatics.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DATABASE_URL or ConnectionStrings:DefaultConnection not configured.");

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(dataSource));

// ── Authentication ────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();

// ── Supabase ──────────────────────────────────────────────────────────────────
var supabaseUrl = builder.Configuration["SUPABASE_URL"]
    ?? throw new InvalidOperationException("SUPABASE_URL not configured.");
var supabaseAnonKey = builder.Configuration["SUPABASE_ANON_KEY"]
    ?? throw new InvalidOperationException("SUPABASE_ANON_KEY not configured.");

var supabase = new Supabase.Client(supabaseUrl, supabaseAnonKey, new Supabase.SupabaseOptions
{
    AutoRefreshToken = false,
    AutoConnectRealtime = false,
});
await supabase.InitializeAsync();
builder.Services.AddSingleton(supabase);

// ── App services ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CollectionService>();
builder.Services.AddHttpClient<BggService>(client =>
{
    client.BaseAddress = new Uri("https://boardgamegeek.com");
    var token = builder.Configuration["BGG_API_TOKEN"];
    if (!string.IsNullOrEmpty(token))
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
});

// ── Blazor + MudBlazor ────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapGet("/healthz", () => Results.Ok("OK"));

// Logout via GET so nav links work (cookie-clear is low-risk)
app.MapGet("/account/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

// Email confirmation from Supabase
app.MapGet("/auth/confirm", async (string? token_hash, string? type, HttpContext http, IConfiguration config, IHttpClientFactory clientFactory) =>
{
    if (string.IsNullOrEmpty(token_hash) || string.IsNullOrEmpty(type))
        return Results.Redirect("/login?notice=Invalid+confirmation+link.");

    var client = clientFactory.CreateClient();
    client.DefaultRequestHeaders.Add("apikey", supabaseAnonKey);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseAnonKey}");

    var response = await client.PostAsJsonAsync(
        $"{supabaseUrl}/auth/v1/verify",
        new { token_hash, type });

    var notice = response.IsSuccessStatusCode
        ? "Email+confirmed!+You+can+now+log+in."
        : "Confirmation+failed.+Please+try+again+or+contact+support.";

    return Results.Redirect($"/login?notice={notice}");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

