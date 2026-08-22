using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormFlow.Web.Models;
using FormFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FormFlow.Web.Pages
{
    /// <summary>Public page reached through the shared link. Fills the first (employee) stage.</summary>
    public class FillModel : PageModel
    {
        private readonly FormWorkflowService _workflow;

        public FillModel(FormWorkflowService workflow)
        {
            _workflow = workflow;
        }

        public FormTemplate Template { get; private set; }

        public FormStage Stage { get; private set; }

        /// <summary>Set when the employee opened the link to correct a returned submission.</summary>
        public Submission ReturnedSubmission { get; private set; }

        public Dictionary<int, string> Answers { get; private set; } = new Dictionary<int, string>();

        public Dictionary<int, string> Errors { get; private set; } = new Dictionary<int, string>();

        public string SubmitterName { get; private set; }

        public string SubmitterEmail { get; private set; }

        public async Task<IActionResult> OnGetAsync(string token, string code)
        {
            var load = await LoadAsync(token, code);
            if (load != null)
            {
                return load;
            }

            if (ReturnedSubmission != null)
            {
                Answers = Stage.Fields.ToDictionary(
                    f => f.Id,
                    f => ReturnedSubmission.Values.FirstOrDefault(v => v.FormFieldId == f.Id)?.Value ?? string.Empty);
                SubmitterName = ReturnedSubmission.SubmitterName;
                SubmitterEmail = ReturnedSubmission.SubmitterEmail;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string token, string code)
        {
            var load = await LoadAsync(token, code);
            if (load != null)
            {
                return load;
            }

            Answers = AnswerBinder.FromForm(Request.Form, Stage);
            SubmitterName = Request.Form["SubmitterName"].ToString().Trim();
            SubmitterEmail = Request.Form["SubmitterEmail"].ToString().Trim();
            Errors = FormWorkflowService.Validate(Stage, Answers);

            if (ReturnedSubmission == null && string.IsNullOrWhiteSpace(SubmitterName))
            {
                ModelState.AddModelError(string.Empty, "الاسم مطلوب");
            }

            if (Errors.Count > 0 || !ModelState.IsValid)
            {
                return Page();
            }

            string trackingCode;
            if (ReturnedSubmission == null)
            {
                var submission = await _workflow.CreateSubmissionAsync(Template, SubmitterName, SubmitterEmail, Answers);
                trackingCode = submission.TrackingCode;
            }
            else
            {
                await _workflow.CompleteStageAsync(
                    ReturnedSubmission,
                    Stage,
                    Answers,
                    ReturnedSubmission.SubmitterName ?? "الموظف",
                    "إعادة إرسال بعد التعديل");
                trackingCode = ReturnedSubmission.TrackingCode;
            }

            return RedirectToPage("/Submitted", new { code = trackingCode });
        }

        private async Task<IActionResult> LoadAsync(string token, string code)
        {
            Template = await _workflow.GetTemplateByTokenAsync(token);
            if (Template == null || !Template.IsPublished)
            {
                return NotFound();
            }

            Template.Stages = Template.Stages.OrderBy(s => s.Order).ToList();
            Stage = Template.Stages.FirstOrDefault();
            if (Stage == null)
            {
                return NotFound();
            }

            Stage.Fields = Stage.Fields.OrderBy(f => f.Order).ToList();

            if (!string.IsNullOrWhiteSpace(code))
            {
                var submission = await _workflow.GetSubmissionByCodeAsync(code);
                if (submission == null || submission.FormTemplateId != Template.Id || submission.Status != SubmissionStatus.Returned)
                {
                    return NotFound();
                }

                ReturnedSubmission = submission;
                Stage = submission.FormTemplate.Stages.First(s => s.Order == Stage.Order);
            }

            return null;
        }
    }
}
