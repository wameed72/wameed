using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using FormFlow.Web.Models;

namespace FormFlow.Web.Services
{
    /// <summary>Arabic labels declared on the enum members.</summary>
    public static class DisplayNames
    {
        public static string Status(SubmissionStatus status) => Of(status);

        public static string Role(StageRole role) => Of(role);

        public static string FieldType(FieldType type) => Of(type);

        public static string Of<TEnum>(TEnum value)
            where TEnum : struct, Enum
        {
            var member = typeof(TEnum).GetMember(value.ToString()).FirstOrDefault();
            var display = member?.GetCustomAttribute<DisplayAttribute>();
            return display?.Name ?? value.ToString();
        }

        public static string StatusBadgeClass(SubmissionStatus status)
        {
            switch (status)
            {
                case SubmissionStatus.Completed:
                    return "badge-done";
                case SubmissionStatus.Returned:
                    return "badge-returned";
                case SubmissionStatus.Rejected:
                    return "badge-rejected";
                default:
                    return "badge-progress";
            }
        }
    }
}
