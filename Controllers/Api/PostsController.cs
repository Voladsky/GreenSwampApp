using System.Text.RegularExpressions;
using GreenSwampApp.Data;
using GreenSwampApp.Models;
using GreenSwampApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GreenSwampApp.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PostsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<Auth> _userManager;

        public PostsController(ApplicationDbContext context, UserManager<Auth> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // === 1. Создание поста (с хэштегами) ===
        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { error = "Content cannot be empty." });

            var auth = await _userManager.GetUserAsync(User);
            if (auth == null) return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == auth.Id);
            if (user == null) return Unauthorized();

            // Извлечение хэштегов
            var hashtags = ExtractHashtags(request.Content);
            var tagEntities = new List<Tag>();

            foreach (var tagName in hashtags)
            {
                var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
                if (tag == null)
                {
                    tag = new Tag { Name = tagName };
                    _context.Tags.Add(tag);
                    await _context.SaveChangesAsync(); // получаем Id
                }
                tagEntities.Add(tag);
            }

            // Создание поста
            var post = new Post
            {
                UserId = user.UserId,
                User = user,
                Content = request.Content,
                CreatedAt = DateTime.UtcNow,
                MediaType = "",
                MediaUrl = "",
                AnswersCount = 0,
                ReribbsCount = 0
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            // Добавление связей PostTag
            foreach (var tag in tagEntities)
            {
                _context.PostTags.Add(new PostTag { PostId = post.PostId, TagId = tag.TagId });
            }
            await _context.SaveChangesAsync();

            // Формируем ViewModel
            var postViewModel = MapToViewModel(post, user);
            return Ok(postViewModel);
        }

        // === 2. Увеличение Reribbs ===
        [HttpPost("{id}/reribb")]
        public async Task<IActionResult> AddReribb(long id)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null) return NotFound();

            var auth = await _userManager.GetUserAsync(User);
            if (auth == null) return Unauthorized();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == auth.Id);
            if (user == null) return Unauthorized();

            // Проверка на повторный рерибб (опционально)
            var existing = await _context.Interactions
                .FirstOrDefaultAsync(i => i.PostId == id && i.UserId == user.UserId && i.InteractionType == "Reribb");
            if (existing != null)
                return BadRequest(new { error = "You have already reribbed this post." });

            // Увеличиваем счётчик
            post.ReribbsCount++;
            // Сохраняем взаимодействие
            _context.Interactions.Add(new Interaction
            {
                PostId = id,
                UserId = user.UserId,
                InteractionType = "Reribb",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return Ok(new { reribbsCount = post.ReribbsCount });
        }

        // === 3. Увеличение Answers (добавление комментария) ===
        [HttpPost("{id}/answer")]
        public async Task<IActionResult> AddAnswer(long id, [FromBody] AddAnswerRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { error = "Answer content cannot be empty." });

            var post = await _context.Posts.FindAsync(id);
            if (post == null) return NotFound();

            var auth = await _userManager.GetUserAsync(User);
            if (auth == null) return Unauthorized();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == auth.Id);
            if (user == null) return Unauthorized();

            // Увеличиваем счётчик
            post.AnswersCount++;
            // Сохраняем комментарий в Interactions
            _context.Interactions.Add(new Interaction
            {
                PostId = id,
                UserId = user.UserId,
                InteractionType = "Answer",
                CommentContent = request.Content,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // Формируем данные для обновления счётчика на клиенте
            var commentViewModel = new CommentViewModel
            {
                User = new UserViewModel
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    AvatarUrl = user.AvatarUrl ?? "/images/default-avatar.png"
                },
                Content = request.Content,
                CreatedAt = DateTime.UtcNow,
                TimeAgo = GetTimeAgo(DateTime.UtcNow)
            };

            return Ok(new { answersCount = post.AnswersCount, comment = commentViewModel });
        }

        // === Вспомогательные методы ===
        private static List<string> ExtractHashtags(string postContent)
        {
            var regex = new Regex(@"#\w+");
            return regex.Matches(postContent)
                        .Cast<Match>()
                        .Select(m => m.Value.TrimStart('#'))
                        .Distinct()
                        .ToList();
        }

        private static string ConvertToHtml(string content)
        {
            if (string.IsNullOrEmpty(content)) return content;
            return Regex.Replace(content, @"#(\w+)", "<a href='/ponds/$1'>#$1</a>");
        }

        private PostViewModel MapToViewModel(Post post, User user)
        {
            return new PostViewModel
            {
                PostId = post.PostId,
                User = new UserViewModel
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    AvatarUrl = user.AvatarUrl ?? "/images/default-avatar.png"
                },
                Content = post.Content,
                CreatedAt = post.CreatedAt,
                TimeAgo = GetTimeAgo(post.CreatedAt),
                AnswersCount = post.AnswersCount,
                ReribbsCount = post.ReribbsCount,
                Tags = post.PostTags?.Select(pt => new TagViewModel { TagId = pt.TagId, Name = pt.Tag.Name }).ToList() ?? new List<TagViewModel>(),
                MediaUrl = post.MediaUrl,
                MediaType = post.MediaType
            };
        }

        private string GetTimeAgo(DateTime dateTime)
        {
            var diff = DateTime.UtcNow - dateTime;
            if (diff.TotalSeconds < 60) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            return $"{diff.Days}d ago";
        }
    }

    public class CreatePostRequest { public string Content { get; set; } }
    public class AddAnswerRequest { public string Content { get; set; } }
}