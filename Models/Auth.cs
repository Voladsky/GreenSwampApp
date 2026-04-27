// Models/Auth.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace GreenSwampApp.Models
{
    [Table("auth")]
    public class Auth : IdentityUser<long>
    {
        // Переопределяем Id для соответствия вашей БД
        [Key]
        [Column("user_id")]
        public override long Id { get; set; }

        [Column("password_hash")]
        public override string? PasswordHash { get; set; }

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }

        [Column("reset_token")]
        public string? ResetToken { get; set; }

        [Column("token_expiry")]
        public DateTime? TokenExpiry { get; set; }
    }
}