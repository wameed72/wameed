using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace FormFlow.Web.Models
{
    public class FormField
    {
        public int Id { get; set; }

        public int FormStageId { get; set; }

        public FormStage FormStage { get; set; }

        public int Order { get; set; }

        [Required(ErrorMessage = "نص السؤال مطلوب")]
        [StringLength(300)]
        [Display(Name = "السؤال")]
        public string Label { get; set; }

        [Display(Name = "نوع الحقل")]
        public FieldType Type { get; set; }

        [Display(Name = "إجابة إلزامية")]
        public bool IsRequired { get; set; }

        [StringLength(300)]
        [Display(Name = "ملاحظة توضيحية")]
        public string HelpText { get; set; }

        /// <summary>Choices for select/radio/checkbox fields, one per line.</summary>
        [StringLength(2000)]
        [Display(Name = "الخيارات (خيار في كل سطر)")]
        public string Options { get; set; }

        public IReadOnlyList<string> OptionList()
        {
            if (string.IsNullOrWhiteSpace(Options))
            {
                return Array.Empty<string>();
            }

            return Options
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(o => o.Trim())
                .Where(o => o.Length > 0)
                .ToList();
        }

        public bool HasOptions => Type == FieldType.Select || Type == FieldType.Radio || Type == FieldType.Checkbox;
    }
}
