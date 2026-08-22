using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FormFlow.Web.Data;
using FormFlow.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace FormFlow.Web.Services
{
    /// <summary>
    /// Drives the multi stage life cycle of a submission: the employee fills the first stage through the
    /// public link, then every following stage is completed by the role that owns it.
    /// </summary>
    public class FormWorkflowService
    {
        private readonly FormFlowDbContext _db;

        public FormWorkflowService(FormFlowDbContext db)
        {
            _db = db;
        }

        public Task<FormTemplate> GetTemplateByTokenAsync(string token)
        {
            return TemplatesWithStages().FirstOrDefaultAsync(t => t.PublicToken == token);
        }

        public Task<FormTemplate> GetTemplateAsync(int id)
        {
            return TemplatesWithStages().FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<Submission> GetSubmissionAsync(int id)
        {
            var submission = await SubmissionsWithDetails().FirstOrDefaultAsync(s => s.Id == id);
            return Sort(submission);
        }

        public async Task<Submission> GetSubmissionByCodeAsync(string trackingCode)
        {
            var code = (trackingCode ?? string.Empty).Trim().ToUpperInvariant();
            var submission = await SubmissionsWithDetails().FirstOrDefaultAsync(s => s.TrackingCode == code);
            return Sort(submission);
        }

        /// <summary>Submissions waiting for a stage owned by <paramref name="role"/>.</summary>
        public async Task<List<Submission>> GetInboxAsync(StageRole role)
        {
            var candidates = await _db.Submissions
                .Include(s => s.FormTemplate).ThenInclude(t => t.Stages)
                .Where(s => s.Status == SubmissionStatus.InProgress)
                .OrderBy(s => s.UpdatedUtc)
                .ToListAsync();

            return candidates
                .Where(s => CurrentStage(s)?.Role == role)
                .ToList();
        }

        public static FormStage CurrentStage(Submission submission)
        {
            return submission?.FormTemplate?.Stages?.FirstOrDefault(st => st.Order == submission.CurrentStageOrder);
        }

        /// <summary>Creates a submission from the answers of the first stage and moves it to the next stage.</summary>
        public async Task<Submission> CreateSubmissionAsync(
            FormTemplate template,
            string submitterName,
            string submitterEmail,
            IDictionary<int, string> answers)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            var firstStage = template.Stages.OrderBy(s => s.Order).FirstOrDefault();
            if (firstStage == null)
            {
                throw new InvalidOperationException("لا تحتوي الاستمارة على أي مرحلة");
            }

            var submission = new Submission
            {
                FormTemplateId = template.Id,
                TrackingCode = await UniqueTrackingCodeAsync(),
                CurrentStageOrder = firstStage.Order,
                Status = SubmissionStatus.InProgress,
                SubmitterName = submitterName,
                SubmitterEmail = submitterEmail,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync();

            await CompleteStageAsync(submission, firstStage, answers, submitterName ?? "الموظف", null);
            return submission;
        }

        /// <summary>Stores the answers of a stage and advances the submission to the next stage.</summary>
        public async Task CompleteStageAsync(
            Submission submission,
            FormStage stage,
            IDictionary<int, string> answers,
            string actor,
            string note)
        {
            if (submission == null)
            {
                throw new ArgumentNullException(nameof(submission));
            }

            if (stage == null)
            {
                throw new ArgumentNullException(nameof(stage));
            }

            await SaveAnswersAsync(submission, stage, answers);

            var template = submission.FormTemplate ?? await GetTemplateAsync(submission.FormTemplateId);
            var nextStage = template.Stages
                .Where(s => s.Order > stage.Order)
                .OrderBy(s => s.Order)
                .FirstOrDefault();

            if (nextStage == null)
            {
                submission.Status = SubmissionStatus.Completed;
            }
            else
            {
                submission.CurrentStageOrder = nextStage.Order;
                submission.Status = SubmissionStatus.InProgress;
            }

            submission.UpdatedUtc = DateTime.UtcNow;
            AddEvent(submission, stage.Id, actor, nextStage == null ? "إكمال الاستمارة" : $"إكمال مرحلة: {stage.Title}", note);
            await _db.SaveChangesAsync();
        }

        /// <summary>Sends the submission back to the employee so the first stage can be corrected.</summary>
        public async Task ReturnToEmployeeAsync(Submission submission, string actor, string note)
        {
            var template = submission.FormTemplate ?? await GetTemplateAsync(submission.FormTemplateId);
            var firstStage = template.Stages.OrderBy(s => s.Order).First();

            submission.CurrentStageOrder = firstStage.Order;
            submission.Status = SubmissionStatus.Returned;
            submission.UpdatedUtc = DateTime.UtcNow;
            AddEvent(submission, firstStage.Id, actor, "إعادة إلى الموظف", note);
            await _db.SaveChangesAsync();
        }

        public async Task RejectAsync(Submission submission, string actor, string note)
        {
            submission.Status = SubmissionStatus.Rejected;
            submission.UpdatedUtc = DateTime.UtcNow;
            AddEvent(submission, CurrentStage(submission)?.Id, actor, "رفض الاستمارة", note);
            await _db.SaveChangesAsync();
        }

        /// <summary>Validates the posted answers of a stage. Returns field id to error message.</summary>
        public static Dictionary<int, string> Validate(FormStage stage, IDictionary<int, string> answers)
        {
            var errors = new Dictionary<int, string>();
            foreach (var field in stage.Fields.OrderBy(f => f.Order))
            {
                answers.TryGetValue(field.Id, out var raw);
                var value = (raw ?? string.Empty).Trim();

                if (value.Length == 0)
                {
                    if (field.IsRequired)
                    {
                        errors[field.Id] = "هذا الحقل مطلوب";
                    }

                    continue;
                }

                var error = ValidateValue(field, value);
                if (error != null)
                {
                    errors[field.Id] = error;
                }
            }

            return errors;
        }

        private static string ValidateValue(FormField field, string value)
        {
            switch (field.Type)
            {
                case FieldType.Number:
                    return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                        ? null
                        : "أدخل رقمًا صحيحًا";

                case FieldType.Date:
                    return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                        ? null
                        : "أدخل تاريخًا صحيحًا";

                case FieldType.Email:
                    var at = value.IndexOf('@');
                    return at > 0 && at < value.Length - 1 && value.IndexOf('.', at) > at + 1
                        ? null
                        : "أدخل بريدًا إلكترونيًا صحيحًا";

                case FieldType.Select:
                case FieldType.Radio:
                    return field.OptionList().Contains(value) ? null : "اختر أحد الخيارات المتاحة";

                case FieldType.Checkbox:
                    var options = field.OptionList();
                    var allKnown = value
                        .Split(new[] { FieldValue.MultiValueSeparator }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim())
                        .All(v => options.Contains(v));
                    return allKnown ? null : "اختر من الخيارات المتاحة فقط";

                default:
                    return null;
            }
        }

        private async Task SaveAnswersAsync(Submission submission, FormStage stage, IDictionary<int, string> answers)
        {
            var stageFieldIds = stage.Fields.Select(f => f.Id).ToHashSet();
            var existing = await _db.FieldValues
                .Where(v => v.SubmissionId == submission.Id && stageFieldIds.Contains(v.FormFieldId))
                .ToListAsync();

            foreach (var field in stage.Fields)
            {
                answers.TryGetValue(field.Id, out var raw);
                var value = (raw ?? string.Empty).Trim();
                var current = existing.FirstOrDefault(v => v.FormFieldId == field.Id);

                if (current == null)
                {
                    _db.FieldValues.Add(new FieldValue
                    {
                        SubmissionId = submission.Id,
                        FormFieldId = field.Id,
                        Value = value,
                        UpdatedUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    current.Value = value;
                    current.UpdatedUtc = DateTime.UtcNow;
                }
            }
        }

        private void AddEvent(Submission submission, int? stageId, string actor, string action, string note)
        {
            _db.SubmissionEvents.Add(new SubmissionEvent
            {
                SubmissionId = submission.Id,
                FormStageId = stageId,
                Actor = string.IsNullOrWhiteSpace(actor) ? "غير معروف" : actor,
                Action = action,
                Note = note,
                CreatedUtc = DateTime.UtcNow
            });
        }

        private async Task<string> UniqueTrackingCodeAsync()
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var code = TokenGenerator.NewTrackingCode();
                if (!await _db.Submissions.AnyAsync(s => s.TrackingCode == code))
                {
                    return code;
                }
            }

            throw new InvalidOperationException("تعذر توليد رمز متابعة فريد");
        }

        private IQueryable<FormTemplate> TemplatesWithStages()
        {
            return _db.FormTemplates
                .Include(t => t.Stages)
                .ThenInclude(s => s.Fields);
        }

        private IQueryable<Submission> SubmissionsWithDetails()
        {
            return _db.Submissions
                .Include(s => s.FormTemplate).ThenInclude(t => t.Stages).ThenInclude(st => st.Fields)
                .Include(s => s.Values)
                .Include(s => s.Events);
        }

        private static Submission Sort(Submission submission)
        {
            if (submission?.FormTemplate?.Stages != null)
            {
                submission.FormTemplate.Stages = submission.FormTemplate.Stages.OrderBy(s => s.Order).ToList();
                foreach (var stage in submission.FormTemplate.Stages)
                {
                    stage.Fields = stage.Fields.OrderBy(f => f.Order).ToList();
                }
            }

            if (submission?.Events != null)
            {
                submission.Events = submission.Events.OrderBy(e => e.CreatedUtc).ToList();
            }

            return submission;
        }
    }
}
