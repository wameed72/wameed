using System.ComponentModel.DataAnnotations;

namespace FormFlow.Web.Models
{
    /// <summary>Staff account (supervisor, manager or administrator). Employees never sign in.</summary>
    public class AppUser
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "اسم المستخدم")]
        public string Username { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "الاسم الظاهر")]
        public string DisplayName { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [Required]
        public string PasswordSalt { get; set; }

        [Display(Name = "الدور")]
        public StageRole Role { get; set; }

        [Display(Name = "مدير النظام")]
        public bool IsAdministrator { get; set; }
    }
}
