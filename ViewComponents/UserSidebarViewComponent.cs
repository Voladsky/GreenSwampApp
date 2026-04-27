using GreenSwampApp.Data;
using GreenSwampApp.Models;
using GreenSwampApp.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GreenSwampApp.ViewComponents
{
    // Move ViewModel OUTSIDE the component class
    public class UserSidebarViewModel
    {
        public UserViewModel User { get; set; }
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
        public IEnumerable<TrendingTagViewModel> TrendingTags { get; set; }
    }

    public class UserSidebarViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Auth> _userManager;

        public UserSidebarViewComponent(ApplicationDbContext context, UserManager<Auth> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var auth = await _userManager.GetUserAsync(UserClaimsPrincipal);
            if (auth == null) return Content(string.Empty);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == auth.Id);
            if (user == null) return Content(string.Empty);

            var followersCount = await _context.Followers.CountAsync(f => f.FollowingId == user.UserId);
            var followingCount = await _context.Followers.CountAsync(f => f.FollowerId == user.UserId);

            var trendingTags = await _context.PostTags
                .GroupBy(pt => pt.TagId)
                .Select(g => new TrendingTagViewModel
                {
                    Name = g.FirstOrDefault().Tag.Name,
                    PostCount = g.Count()
                })
                .OrderByDescending(t => t.PostCount)
                .Take(3)
                .ToListAsync();

            var model = new UserSidebarViewModel
            {
                User = new UserViewModel
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    AvatarUrl = user.AvatarUrl ?? "/images/default-avatar.jpg"
                },
                FollowersCount = followersCount,
                FollowingCount = followingCount,
                TrendingTags = trendingTags
            };

            return View(model); // This looks for Default.cshtml
        }
    }
}