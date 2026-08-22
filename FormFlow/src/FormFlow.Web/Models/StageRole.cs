using System.ComponentModel.DataAnnotations;

namespace FormFlow.Web.Models
{
    /// <summary>Who is allowed to fill a stage. <see cref="Employee"/> stages are filled through the public link.</summary>
    public enum StageRole
    {
        [Display(Name = "الموظف (رابط عام)")]
        Employee = 0,

        [Display(Name = "المشرف")]
        Supervisor = 1,

        [Display(Name = "المدير")]
        Manager = 2
    }
}
