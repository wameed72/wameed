using System.Threading.Tasks;
using FormFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FormFlow.Web.Pages.Admin
{
    public class SubmissionModel : PageModel
    {
        private readonly FormWorkflowService _workflow;

        public SubmissionModel(FormWorkflowService workflow)
        {
            _workflow = workflow;
        }

        public Models.Submission Submission { get; private set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Submission = await _workflow.GetSubmissionAsync(id);
            if (Submission == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}
