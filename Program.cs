using GreenSwampApp;
using GreenSwampApp.Data;
using GreenSwampApp.Models;
using GreenSwampApp.Services;
using GreenSwampApp.ViewComponents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Добавляем Identity с вашим кастомным Auth классом
builder.Services.AddIdentity<Auth, IdentityRole<long>>(options =>
{
    // Настройки пароля
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Настройки блокировки
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // Настройки пользователя
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddScoped<UserSidebarViewComponent>();

builder.Services.AddRazorPages();

builder.Services.AddScoped<ICsvExportService, CsvExportService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Настройки SMTP из appsettings.json
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseMiddleware<LoggingMiddleware>();

app.UseRouting();

app.UseAuthorization();

app.UseStatusCodePagesWithReExecute("/NotFound");

app.MapControllerRoute(
    name: "profile",
    pattern: "profile/{username}",
    defaults: new { controller = "Profile", action = "Index" });

app.MapControllerRoute(
    name: "postDetail",
    pattern: "feed/post/{postId}",
    defaults: new { controller = "Feed", action = "PostDetail" });

app.MapControllerRoute(
    name: "ponds",
    pattern: "ponds/{tag}",
    defaults: new { controller = "Ponds", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Feed}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

[Route("/subscribe")]
[ApiController]
public class SubscribeController : ControllerBase
{
    private readonly IEmailService _emailService;

    public SubscribeController(IEmailService emailService)
    {
        _emailService = emailService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe([FromForm] EmailRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await _emailService.SendSubscriptionConfirmationAsync(request.Email);
        return Ok(new { message = "Subscription successful" });
    }
}

public class EmailRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; }
}