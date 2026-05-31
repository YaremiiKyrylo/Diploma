using System.Text;
using AIChatAssistant.Application.DI;
using AIChatAssistant.Application.Services.LLM;
using AIChatAssistant.Domain.Entities;
using AIChatAssistant.Domain.Service_Interfaces;
using AIChatAssistant.Infrastructure.DI;
using AIChatAssistant.Infrastructure.Persistence.AiServiceDbContext;
using AIChatAssistant.Shared.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.Configure<LLMConfiguration>(builder.Configuration.GetSection("LLM"));
builder.Services.AddHttpClient<ILlmCallService, GroqCallService>();

builder.Services.Configure<AiModelSettings>(options =>
{
    builder.Configuration.GetSection("AI").Bind(options);
    options.TokenizerJsonPath = ResolveAiModelPath(options.TokenizerJsonPath, builder.Environment.ContentRootPath);
    options.EmbeddingModelPath = ResolveAiModelPath(options.EmbeddingModelPath, builder.Environment.ContentRootPath);
});
builder.Services.Configure<ApplicationSettings>(options =>
    builder.Configuration.GetSection(ApplicationSettings.SectionName));
builder.Services.Configure<PineconeSettings>(
    builder.Configuration.GetSection(PineconeSettings.SectionName));

builder.Services
    .AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AiServiceDbContext>()
    .AddDefaultTokenProviders();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"]
    ?? throw new InvalidOperationException("JwtSettings:Secret is missing");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

await SeedRolesAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static string ResolveAiModelPath(string path, string contentRoot)
{
    if (string.IsNullOrWhiteSpace(path))
        return path;
    if (Path.IsPathRooted(path))
        return Path.GetFullPath(path);

    var outputPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    if (File.Exists(outputPath))
        return outputPath;

    return Path.GetFullPath(Path.Combine(contentRoot, path));
}

static async Task SeedRolesAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

    foreach (var roleName in new[] { "Admin", "User" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
    }
}
