using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FormFlow.Web.Models
{
    public class FormStage
    {
        public int Id { get; set; }

        public int FormTemplateId { get; set; }

        public FormTemplate FormTemplate { get; set; }

        /// <summary>1-based position of the stage inside the template.</summary>
        [Display(Name = "الترتيب")]
        public int Order { get; set; }

        [Required(ErrorMessage = "عنوان المرحلة مطلوب")]
        [StringLength(200)]
        [Display(Name = "عنوان المرحلة")]
        public string Title { get; set; }

        [StringLength(500)]
        [Display(Name = "تعليمات المرحلة")]
        public string Instructions { get; set; }

        [Display(Name = "الجهة المسؤولة")]
        public StageRole Role { get; set; }

        public List<FormField> Fields { get; set; } = new List<FormField>();
    }
}
