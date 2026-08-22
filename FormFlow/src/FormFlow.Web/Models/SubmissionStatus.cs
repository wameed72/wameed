using System.ComponentModel.DataAnnotations;

namespace FormFlow.Web.Models
{
    public enum SubmissionStatus
    {
        [Display(Name = "قيد الإنجاز")]
        InProgress = 0,

        [Display(Name = "مُعادة للموظف")]
        Returned = 1,

        [Display(Name = "مكتملة")]
        Completed = 2,

        [Display(Name = "مرفوضة")]
        Rejected = 3
    }
}
