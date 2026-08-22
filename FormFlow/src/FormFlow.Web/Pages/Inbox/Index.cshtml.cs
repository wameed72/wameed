using System.Collections.Generic;
using System.Threading.Tasks;
using FormFlow.Web.Models;
using FormFlow.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FormFlow.Web.Pages.Inbox
{
    public class IndexModel : PageModel
    {
        private readonly FormWorkflowService _workflow;

        public IndexModel(FormWorkflowService workflow)
        {
            _workflow = workflow;
        }

        public StageRole Role { get; private set; }

        public List<Submission> Pending { get; private set; } = new List<Submission>();

        public async Task OnGetAsync()
        {
            Role = UserService.RoleOf(User);
            Pending = await _workflow.GetInboxAsync(Role);
        }
    }
}
