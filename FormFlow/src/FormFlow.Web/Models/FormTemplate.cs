using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FormFlow.Web.Models
{
    public class FormTemplate
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "العنوان مطلوب")]
        [StringLength(200)]
        [Display(Name = "عنوان الاستمارة")]
        public string Title { get; set; }

        [StringLength(1000)]
        [Display(Name = "الوصف")]
        public string Description { get; set; }

        /// <summary>Random token used in the public fill link so the id is not guessable.</summary>
        [Required]
        [StringLength(32)]
        public string PublicToken { get; set; }

        [Display(Name = "منشورة")]
        public bool IsPublished { get; set; }

        public DateTime CreatedUtc { get; set; }

        public List<FormStage> Stages { get; set; } = new List<FormStage>();

        public List<Submission> Submissions { get; set; } = new List<Submission>();
    }
}
