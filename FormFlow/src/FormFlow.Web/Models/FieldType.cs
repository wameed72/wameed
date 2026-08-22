using System.ComponentModel.DataAnnotations;

namespace FormFlow.Web.Models
{
    public enum FieldType
    {
        [Display(Name = "نص قصير")]
        Text = 0,

        [Display(Name = "نص طويل")]
        LongText = 1,

        [Display(Name = "رقم")]
        Number = 2,

        [Display(Name = "تاريخ")]
        Date = 3,

        [Display(Name = "بريد إلكتروني")]
        Email = 4,

        [Display(Name = "قائمة منسدلة")]
        Select = 5,

        [Display(Name = "اختيار واحد")]
        Radio = 6,

        [Display(Name = "اختيار متعدد")]
        Checkbox = 7
    }
}
