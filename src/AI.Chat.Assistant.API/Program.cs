using AIChatAssistant.Extensions;
using AIChatAssistant.Middleware;
using AIChatAssistant.Shared.Configuration;
using AIChatAssistant.Infrastructure.DI;
using AIChatAssistant.Application.DI;
using AIChatAssistant.Application.Services.LLM;
using AIChatAssistant.API.Swagger;
using AIChatAssistant.Domain.Service_Interfaces;
using Serilog;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();
try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext());

    // register application & infrastructure
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.Configure<LLMConfiguration>(builder.Configuration.GetSection("LLM"));

    builder.Services.AddHttpClient();
    builder.Services.AddHttpClient<ILlmCallService, GroqCallService>();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.OperationFilter<FileUploadRequestOperationFilter>();
    });

    builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing")))
        };
    });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
    });

    // strongly-typed options — resolve model paths relative to ContentRootPath
    var contentRoot = builder.Environment.ContentRootPath;
    builder.Services.Configure<AiModelSettings>(options =>
    {
        builder.Configuration.GetSection("AI").Bind(options);
        if (!Path.IsPathRooted(options.TokenizerJsonPath))
            options.TokenizerJsonPath = Path.Combine(contentRoot, options.TokenizerJsonPath);
        if (!Path.IsPathRooted(options.EmbeddingModelPath))
            options.EmbeddingModelPath = Path.Combine(contentRoot, options.EmbeddingModelPath);
    });
    builder.Services.Configure<ApplicationSettings>(
        builder.Configuration.GetSection(ApplicationSettings.SectionName));
    builder.Services.Configure<PineconeSettings>(
        builder.Configuration.GetSection(PineconeSettings.SectionName));

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCustomExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseMiddleware<ApiKeyAuthMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Diagnostic endpoint — protected, dev-only
    if (app.Environment.IsDevelopment())
    {
        app.MapGet("/api/diag", (IWebHostEnvironment env, IConfiguration config) =>
        {
            var tokPath = config["AI:TokenizerJsonPath"] ?? "not set";
            var modelPath = config["AI:EmbeddingModelPath"] ?? "not set";
            var resolvedTok = Path.IsPathRooted(tokPath) ? tokPath : Path.Combine(env.ContentRootPath, tokPath);
            var resolvedModel = Path.IsPathRooted(modelPath) ? modelPath : Path.Combine(env.ContentRootPath, modelPath);

            return Results.Ok(new
            {
                tokenizerExists = File.Exists(resolvedTok),
                modelExists = File.Exists(resolvedModel)
            });
        });
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}