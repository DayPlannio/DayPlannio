using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication("Cliente")
    .AddCookie("Cliente", options =>
    {
        options.Cookie.Name = "DayPlannio.Web";
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Cliente", policy => policy.RequireClaim("tipo", "cliente"));
    options.AddPolicy("Admin", policy => policy.RequireClaim("tipo", "admin"));
});

builder.Services.AddControllersWithViews();

builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5143/");
    client.DefaultRequestHeaders.Add("X-Api-Key", builder.Configuration["Api:Key"] ?? "");
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "compat-login", pattern: "Login", defaults: new { controller = "Account", action = "Login" });
app.MapControllerRoute(name: "compat-metri", pattern: "Metricas", defaults: new { controller = "Dashboard", action = "Metricas" });
app.MapDefaultControllerRoute();

app.Run();

