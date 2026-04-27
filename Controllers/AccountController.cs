// Controllers/AccountController.cs
using GreenSwampApp.Data;
using GreenSwampApp.Models;
using GreenSwampApp.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GreenSwampApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<Auth> _userManager;
        private readonly SignInManager<Auth> _signInManager;
        private readonly ILogger<AccountController> _logger;
        private readonly ApplicationDbContext _context;

        public AccountController(
            UserManager<Auth> userManager,
            SignInManager<Auth> signInManager,
            ILogger<AccountController> logger,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
        }

        [HttpGet]
        [Route("/Login")]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("/Login")]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password");
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName,
                model.Password,
                model.RememberMe,  // Use the RememberMe from the form
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                // Update last login time
                user.LastLogin = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                _logger.LogInformation($"User {user.Email} logged in");

                // Use the returnUrl from the form if provided
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return Redirect("/feed");
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning($"Account {user.Email} is locked out");
                ModelState.AddModelError(string.Empty, "Account temporarily locked. Please try again later");
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Invalid email or password");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        [HttpGet]
        [Route("/Register")]
        public IActionResult Register(string? returnUrl = null)
        {
            var model = new RegisterViewModel { ReturnUrl = returnUrl };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("/Register")]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Check uniqueness
            if (await _userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError("Email", "User with this email already exists");
                return View(model);
            }
            if (await _userManager.FindByNameAsync(model.FullName) != null)
            {
                ModelState.AddModelError("FullName", "Username already taken");
                return View(model);
            }

            // Create Identity user (Auth)
            var authUser = new Auth
            {
                UserName = model.FullName,
                Email = model.Email,
                LastLogin = DateTime.UtcNow,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(authUser, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            // Create associated User record
            var user = new User
            {
                UserId = authUser.Id,          // same ID as Auth
                Username = model.FullName,
                Email = model.Email,
                PasswordHash = authUser.PasswordHash,
                Bio = "",
                AvatarUrl = "",
                CoverImageUrl = "",
                DisplayName = model.FullName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();   // ✅ Now saved

            await _signInManager.SignInAsync(authUser, isPersistent: false);
            _logger.LogInformation($"New user registered: {model.Email}");

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Feed");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("/Logout")]
        public async Task<IActionResult> Logout(string? returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out");

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToPage("/Index");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}