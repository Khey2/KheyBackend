using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KheyBackend.Models
{
    public class RevokedToken
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }


        [Required]
        public string Jti { get; set; } = string.Empty;


        public DateTime RevokedAt { get; set; } = DateTime.UtcNow;



        [Required]
        public DateTime ExpiresAt { get; set; }

    }
}
