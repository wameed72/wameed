using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FormFlow.Web.Pages
{
    public class SubmittedModel : PageModel
    {
        public string Code { get; private set; }

        public IActionResult OnGet(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return RedirectToPage("/Index");
            }

            Code = code;
            return Page();
        }
    }
}
