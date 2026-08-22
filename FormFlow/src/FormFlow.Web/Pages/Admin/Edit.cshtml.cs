using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormFlow.Web.Data;
using FormFlow.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FormFlow.Web.Pages.Admin
{
    /// <summary>Form builder: manages the stages of a template and the questions of every stage.</summary>
    public class EditModel : PageModel
    {
        private readonly FormFlowDbContext _db;

        public EditModel(FormFlowDbContext db)
        {
            _db = db;
        }

        public FormTemplate Template { get; private set; }

        public IReadOnlyList<StageRole> Roles { get; } = Enum.GetValues(typeof(StageRole)).Cast<StageRole>().ToList();

        public IReadOnlyList<FieldType> FieldTypes { get; } = Enum.GetValues(typeof(FieldType)).Cast<FieldType>().ToList();

        /// <summary>Url of a page handler, used as <c>formaction</c> for the secondary buttons of a form.</summary>
        public string HandlerUrl(string handler) => Url.Page("/Admin/Edit", handler, new { id = Template.Id });

        public string PublicLink() =>
            $"{Request.Scheme}://{Request.Host}{Url.Page("/Fill", new { token = Template.PublicToken })}";

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await LoadAsync(id))
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveTemplateAsync(int id, string title, string description)
        {
            if (!await LoadAsync(id))
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                Template.Title = title.Trim();
            }

            Template.Description = (description ?? string.Empty).Trim();
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "تم حفظ بيانات الاستمارة.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostAddStageAsync(int id, string title, StageRole role, string instructions)
        {
            if (!await LoadAsync(id))
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return RedirectToPage(new { id });
            }

            var nextOrder = Template.Stages.Count == 0 ? 1 : Template.Stages.Max(s => s.Order) + 1;
            _db.FormStages.Add(new FormStage
            {
                FormTemplateId = Template.Id,
                Order = nextOrder,
                Title = title.Trim(),
                Role = role,
                Instructions = (instructions ?? string.Empty).Trim()
            });

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "تمت إضافة المرحلة.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostUpdateStageAsync(int id, int stageId, string title, StageRole role, string instructions)
        {
            if (!await LoadAsync(id))
            {
                return NotFound();
            }

            var stage = Template.Stages.FirstOrDefault(s => s.Id == stageId);
            if (stage == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                stage.Title = title.Trim();
            }

            stage.Role = role;
            stage.Instructions = (instructions ?? string.Empty).Trim();
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "تم حفظ المرحلة.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostDeleteStageAsync(int id, int stageId)
        {
            if (!await LoadAsync(id))
            {
                return NotFound();
            }

            var stage = Template.Stages.FirstOrDefault(s => s.Id == stageId);
            if (stage == null)
            {
                return NotFound();
            }

            _db.FormStages.Remove(stage);
            await _db.SaveChangesAsync();
            await ReorderStagesAsync(Template.Id);
            TempData["StatusMessage"] = "تم حذف المرحلة.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostMoveStageAsync(int id, int stageId, int direction)
        {
            if (!await LoadAsync(id))
            {
                return NotFound();
            }

            var ordered = Template.Stages.OrderBy(s => s.Order).ToList();
            var index = ordered.FindIndex(s => s.Id == stageId);
            var target = index + Math.Sign(direction);
            if (index < 0 || target < 0 || target >= ordered.Count)
            {
                return RedirectToPage(new { id });
            }

            (ordered[index].Order, ordered[target].Order) = (ordered[target].Order, ordered[index].Order);
            await _db.SaveChangesAsync();
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostAddFieldAsync(
            int id, int stageId, string label, FieldType type, bool isRequired, string options, string helpText)
        {
            if (!await LoadAsync(id))
            {
                return NotFound();
            }

            var stage = Template.Stages.FirstOrDefault(s => s.Id == stageId);
            if (stage == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                return RedirectToPage(new { id });
            }

            var nextOrder = stage.Fields.Count == 0 ? 1 : stage.Fields.Max(f => f.Order) + 1;
            _db.FormFields.Add(new FormField
            {
                FormStageId = stage.Id,
                Order = nextOrder,
                Label = label.Trim(),
                Type = type,
                IsRequired = isRequired,
                HelpText = (helpText ?? string.Empty).Trim(),
                Options = NormalizeOptions(options)
            });

            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = "تمت إضافة السؤال.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostDeleteFieldAsync(int id, int fieldId)
        {
            var field = await _db.FormFields.FirstOrDefaultAsync(f => f.Id == fieldId);
            if (field == null)
            {
                return NotFound();
            }

            var stageId = field.FormStageId;
            _db.FormFields.Remove(field);
            await _db.SaveChangesAsync();
            await ReorderFieldsAsync(stageId);
            TempData["StatusMessage"] = "تم حذف السؤال.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostMoveFieldAsync(int id, int fieldId, int direction)
        {
            var field = await _db.FormFields.FirstOrDefaultAsync(f => f.Id == fieldId);
            if (field == null)
            {
                return NotFound();
            }

            var siblings = await _db.FormFields
                .Where(f => f.FormStageId == field.FormStageId)
                .OrderBy(f => f.Order)
                .ToListAsync();

            var index = siblings.FindIndex(f => f.Id == fieldId);
            var target = index + Math.Sign(direction);
            if (target < 0 || target >= siblings.Count)
            {
                return RedirectToPage(new { id });
            }

            (siblings[index].Order, siblings[target].Order) = (siblings[target].Order, siblings[index].Order);
            await _db.SaveChangesAsync();
            return RedirectToPage(new { id });
        }

        /// <summary>Accepts options separated by comma or new line and stores them one per line.</summary>
        private static string NormalizeOptions(string options)
        {
            if (string.IsNullOrWhiteSpace(options))
            {
                return string.Empty;
            }

            var parts = options
                .Split(new[] { ',', '\r', '\n', '،' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0);

            return string.Join("\n", parts);
        }

        private async Task ReorderStagesAsync(int templateId)
        {
            var stages = await _db.FormStages
                .Where(s => s.FormTemplateId == templateId)
                .OrderBy(s => s.Order)
                .ToListAsync();

            for (var i = 0; i < stages.Count; i++)
            {
                stages[i].Order = i + 1;
            }

            await _db.SaveChangesAsync();
        }

        private async Task ReorderFieldsAsync(int stageId)
        {
            var fields = await _db.FormFields
                .Where(f => f.FormStageId == stageId)
                .OrderBy(f => f.Order)
                .ToListAsync();

            for (var i = 0; i < fields.Count; i++)
            {
                fields[i].Order = i + 1;
            }

            await _db.SaveChangesAsync();
        }

        private async Task<bool> LoadAsync(int id)
        {
            Template = await _db.FormTemplates
                .Include(t => t.Stages)
                .ThenInclude(s => s.Fields)
                .FirstOrDefaultAsync(t => t.Id == id);

            return Template != null;
        }
    }
}
