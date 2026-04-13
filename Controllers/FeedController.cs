using GreenSwampApp.Data;
using GreenSwampApp.Models;
using GreenSwampApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GreenSwampApp.Controllers
{
    public class FeedController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FeedController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var feedItems = new List<FeedItemViewModel>();

            // Get posts with their interactions and tags
            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Interactions)
                .Include(p => p.PostTags)
                    .ThenInclude(pt => pt.Tag)
                .Where(p => p.Event == null) // Exclude posts that are events (events have their own display)
                .OrderByDescending(p => p.CreatedAt)
                .Take(20)
                .ToListAsync();

            foreach (var post in posts)
            {
                feedItems.Add(new FeedItemViewModel
                {
                    Type = "post",
                    Post = MapToPostViewModel(post),
                    CreatedAt = post.CreatedAt
                });
            }

            // Get events with their associated posts
            var events = await _context.Events
                .Include(e => e.Post)
                    .ThenInclude(p => p.User)
                .Include(e => e.User)
                .OrderByDescending(e => e.CreatedAt)
                .Take(10)
                .ToListAsync();

            foreach (var evt in events)
            {
                feedItems.Add(new FeedItemViewModel
                {
                    Type = "event",
                    Event = MapToEventViewModel(evt),
                    CreatedAt = evt.CreatedAt
                });
            }

            // Sort all items by date
            feedItems = feedItems.OrderByDescending(i => i.CreatedAt).ToList();

            // Get trending tags
            var trendingTags = await _context.PostTags
                .Include(pt => pt.Tag)
                .GroupBy(pt => pt.Tag)
                .Select(g => new TrendingTagViewModel
                {
                    Name = g.Key.Name,
                    PostCount = g.Count()
                })
                .OrderByDescending(t => t.PostCount)
                .Take(5)
                .ToListAsync();

            // Get upcoming events
            var upcomingEvents = await _context.Events
                .Where(e => e.StartTime > DateTime.UtcNow)
                .OrderBy(e => e.StartTime)
                .Take(3)
                .Select(e => new UpcomingEventViewModel
                {
                    Title = e.Title,
                    StartTime = e.StartTime,
                    Location = e.Location
                })
                .ToListAsync();

            var viewModel = new FeedViewModel
            {
                FeedItems = feedItems,
                TrendingTags = trendingTags,
                UpcomingEvents = upcomingEvents
            };

            return View(viewModel);
        }
        
        public async Task<IActionResult> PostDetail(long postId)
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Interactions)
                .Include(p => p.PostTags)
                    .ThenInclude(pt => pt.Tag)
                .FirstOrDefaultAsync(p => p.PostId == postId);

            if (post == null)
            {
                return NotFound();
            }

            var comments = await _context.Interactions
                .Include(i => i.User)
                .Where(i => i.PostId == postId && i.InteractionType == "comment")
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new CommentViewModel
                {
                    User = new UserViewModel
                    {
                        UserId = i.User.UserId,
                        Username = i.User.Username,
                        DisplayName = i.User.DisplayName,
                        AvatarUrl = i.User.AvatarUrl
                    },
                    Content = i.CommentContent,
                    CreatedAt = i.CreatedAt,
                    TimeAgo = GetTimeAgo(i.CreatedAt)
                })
                .ToListAsync();

            var trendingTags = await GetTrendingTagsAsync();

            var viewModel = new PostDetailViewModel
            {
                Post = MapToPostViewModel(post),
                Comments = comments,
                TrendingTags = trendingTags
            };

            return View(viewModel);
        }

        private PostViewModel MapToPostViewModel(Post post)
        {
            var answersCount = post.Interactions?.Count(i => i.InteractionType == "comment") ?? 0;
            var reribbsCount = post.Interactions?.Count(i => i.InteractionType == "reribb") ?? 0;

            return new PostViewModel
            {
                PostId = post.PostId,
                User = new UserViewModel
                {
                    UserId = post.User.UserId,
                    Username = post.User.Username,
                    DisplayName = post.User.DisplayName,
                    AvatarUrl = post.User.AvatarUrl ?? $"https://i.pravatar.cc/100?u={post.User.Username}@greenswamp.com"
                },
                Content = ParseContentWithHashtags(post.Content),
                MediaUrl = post.MediaUrl,
                MediaType = post.MediaType,
                CreatedAt = post.CreatedAt,
                TimeAgo = GetTimeAgo(post.CreatedAt),
                AnswersCount = answersCount,
                ReribbsCount = reribbsCount,
                Tags = post.PostTags?.Select(pt => new TagViewModel
                {
                    TagId = pt.Tag.TagId,
                    Name = pt.Tag.Name
                }) ?? new List<TagViewModel>()
            };
        }

        private EventViewModel MapToEventViewModel(Event evt)
        {
            return new EventViewModel
            {
                EventId = evt.EventId,
                PostId = evt.PostId,
                Title = evt.Title,
                Description = evt.Description,
                Location = evt.Location,
                StartTime = evt.StartTime,
                EndTime = evt.EndTime,
                Host = new UserViewModel
                {
                    UserId = evt.User.UserId,
                    Username = evt.User.Username,
                    DisplayName = evt.User.DisplayName,
                    AvatarUrl = evt.User.AvatarUrl ?? $"https://i.pravatar.cc/100?u={evt.User.Username}@greenswamp.com"
                }
            };
        }

        private string ParseContentWithHashtags(string content)
        {
            if (string.IsNullOrEmpty(content)) return content;

            // Replace hashtags with links
            return System.Text.RegularExpressions.Regex.Replace(
                content,
                @"#(\w+)",
                "<a href='/ponds/$1' class='text-swamp-600 hover:text-swamp-800 hover:underline'>#$1</a>"
            );
        }

        private static string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.UtcNow - dateTime;

            if (timeSpan.TotalMinutes < 1) return "just now";
            if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes}m";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours}h";
            if (timeSpan.TotalDays < 7) return $"{(int)timeSpan.TotalDays}d";

            return dateTime.ToString("MMM d");
        }

        private async Task<IEnumerable<TrendingTagViewModel>> GetTrendingTagsAsync()
        {
            return await _context.PostTags
                .Include(pt => pt.Tag)
                .GroupBy(pt => pt.Tag)
                .Select(g => new TrendingTagViewModel
                {
                    Name = g.Key.Name,
                    PostCount = g.Count()
                })
                .OrderByDescending(t => t.PostCount)
                .Take(5)
                .ToListAsync();
        }
    }
}