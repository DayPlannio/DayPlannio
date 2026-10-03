using DayPlannio.Api.Data;
using DayPlannio.Api.Models;
using DayPlannio.Api.Services;
using DayPlannio.Api.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<DayPlannioContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DayPlannioContext")
        ?? throw new InvalidOperationException("Connection string 'DayPlannioContext' not found.")));

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ExigirAssinaturaAtivaFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddSingleton<ContextMongodb>();

ContextMongodb.ConnectionString = builder.Configuration.GetSection("MongoConnection:ConnectionString").Value;
ContextMongodb.Database = builder.Configuration.GetSection("MongoConnection:Database").Value;
ContextMongodb.IsSSL = Convert.ToBoolean(builder.Configuration.GetSection("MongoConnection:IsSSL").Value);

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>()
    .AddMongoDbStores<ApplicationUser, ApplicationRole, Guid>(
        ContextMongodb.ConnectionString, ContextMongodb.Database)
    .AddDefaultTokenProviders();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<AdminSettings>(builder.Configuration.GetSection("AdminSettings"));
builder.Services.AddTransient<EmailService>();
builder.Services.AddScoped<RelatorioPdfService>();
builder.Services.AddScoped<LogService>();
builder.Services.AddSingleton<AzureBlobStorageService>();
builder.Services.AddHostedService<PlanoRenovacaoService>();

Console.OutputEncoding = System.Text.Encoding.UTF8;

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var adminConfig = builder.Configuration.GetSection("AdminSettings").Get<AdminSettings>();

    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new ApplicationRole { Name = "Admin", NormalizedName = "ADMIN" });
    }

    if (adminConfig != null &&
        !string.IsNullOrWhiteSpace(adminConfig.Email) &&
        !string.IsNullOrWhiteSpace(adminConfig.Senha))
    {
        var adminUser = await userManager.FindByEmailAsync(adminConfig.Email);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminConfig.Email.Replace("@", "").Replace(".", ""),
                NormalizedUserName = adminConfig.Email.Replace("@", "").Replace(".", "").ToUpper(),
                Email = adminConfig.Email,
                NormalizedEmail = adminConfig.Email.ToUpper(),
                NomeCompleto = adminConfig.Nome,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(adminUser, adminConfig.Senha);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
    }
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseMiddleware<DayPlannio.Api.Services.WebApiKeyMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}

