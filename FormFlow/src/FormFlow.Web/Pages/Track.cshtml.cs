using System.Threading.Tasks;
using FormFlow.Web.Models;
using FormFlow.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FormFlow.Web.Pages
{
    public class TrackModel : PageModel
    {
        private readonly FormWorkflowService _workflow;

        public TrackModel(FormWorkflowService workflow)
        {
            _workflow = workflow;
        }

        public string Code { get; private set; }

        public Submission Submission { get; private set; }

        public bool CodeNotFound { get; private set; }

        public async Task OnGetAsync(string code)
        {
            Code = code;
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            Submission = await _workflow.GetSubmissionByCodeAsync(code);
            CodeNotFound = Submission == null;
        }
    }
}
