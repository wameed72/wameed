using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormFlow.Web.Models;
using FormFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FormFlow.Web.Pages.Inbox
{
    /// <summary>Lets the role that owns the current stage fill it, return the submission or reject it.</summary>
    public class StageModel : PageModel
    {
        private readonly FormWorkflowService _workflow;

        public StageModel(FormWorkflowService workflow)
        {
            _workflow = workflow;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        public Submission Submission { get; private set; }

        public FormStage Stage { get; private set; }

        public Dictionary<int, string> Answers { get; private set; } = new Dictionary<int, string>();

        public Dictionary<int, string> Errors { get; private set; } = new Dictionary<int, string>();

        public string Note { get; private set; }

        /// <summary>Url of a page handler, used as <c>formaction</c> for the secondary buttons of the form.</summary>
        public string HandlerUrl(string handler) => Url.Page("/Inbox/Stage", handler, new { id = Id });

        public async Task<IActionResult> OnGetAsync()
        {
            var load = await LoadAsync();
            if (load != null)
            {
                return load;
            }

            Answers = Stage.Fields.ToDictionary(
                f => f.Id,
                f => Submission.Values.FirstOrDefault(v => v.FormFieldId == f.Id)?.Value ?? string.Empty);

            return Page();
        }

        /// <summary>Posts without a handler (for example a resubmitted browser form) simply re-render the stage.</summary>
        public Task<IActionResult> OnPostAsync() => OnGetAsync();

        public async Task<IActionResult> OnPostCompleteAsync()
        {
            var load = await LoadAsync();
            if (load != null)
            {
                return load;
            }

            Answers = AnswerBinder.FromForm(Request.Form, Stage);
            Note = Request.Form["Note"].ToString().Trim();
            Errors = FormWorkflowService.Validate(Stage, Answers);

            if (Errors.Count > 0)
            {
                return Page();
            }

            await _workflow.CompleteStageAsync(Submission, Stage, Answers, UserService.DisplayName(User), Note);
            TempData["StatusMessage"] = $"تم اعتماد الاستمارة {Submission.TrackingCode}.";
            return RedirectToPage("/Inbox/Index");
        }

        public async Task<IActionResult> OnPostReturnAsync()
        {
            var load = await LoadAsync();
            if (load != null)
            {
                return load;
            }

            Note = Request.Form["Note"].ToString().Trim();
            await _workflow.ReturnToEmployeeAsync(Submission, UserService.DisplayName(User), Note);
            TempData["StatusMessage"] = $"تم إعادة الاستمارة {Submission.TrackingCode} إلى الموظف.";
            return RedirectToPage("/Inbox/Index");
        }

        public async Task<IActionResult> OnPostRejectAsync()
        {
            var load = await LoadAsync();
            if (load != null)
            {
                return load;
            }

            Note = Request.Form["Note"].ToString().Trim();
            await _workflow.RejectAsync(Submission, UserService.DisplayName(User), Note);
            TempData["StatusMessage"] = $"تم رفض الاستمارة {Submission.TrackingCode}.";
            return RedirectToPage("/Inbox/Index");
        }

        private async Task<IActionResult> LoadAsync()
        {
            Submission = await _workflow.GetSubmissionAsync(Id);
            if (Submission == null || Submission.Status != SubmissionStatus.InProgress)
            {
                return NotFound();
            }

            Stage = FormWorkflowService.CurrentStage(Submission);
            if (Stage == null)
            {
                return NotFound();
            }

            if (Stage.Role != UserService.RoleOf(User) && !UserService.IsAdministrator(User))
            {
                return Forbid();
            }

            return null;
        }
    }
}
