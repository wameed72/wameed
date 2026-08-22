using FormFlow.Web.Models;

namespace FormFlow.Web.Pages.Shared
{
    public class FieldInputModel
    {
        public FormField Field { get; set; }

        public string Value { get; set; }

        public string Error { get; set; }

        public bool ReadOnly { get; set; }
    }
}
