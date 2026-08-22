using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormFlow.Web.Data;
using FormFlow.Web.Models;
using FormFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FormFlow.Web.Pages.Admin
{
    public class IndexModel : PageModel
    {
        private readonly FormFlowDbContext _db;

        public IndexModel(FormFlowDbContext db)
        {
            _db = db;
        }

        public List<FormTemplate> Templates { get; private set; } = new List<FormTemplate>();

        public Dictionary<int, int> SubmissionCounts { get; private set; } = new Dictionary<int, int>();

        public int SubmissionCount(int templateId) =>
            SubmissionCounts.TryGetValue(templateId, out var count) ? count : 0;

        public string PublicLink(FormTemplate template) =>
            $"{Request.Scheme}://{Request.Host}{Url.Page("/Fill", new { token = template.PublicToken })}";

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync(string title, string description)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                await LoadAsync();
                ModelState.AddModelError(string.Empty, "العنوان مطلوب");
                return Page();
            }

            var template = new FormTemplate
            {
                Title = title.Trim(),
                Description = (description ?? string.Empty).Trim(),
                PublicToken = TokenGenerator.NewToken(),
                CreatedUtc = DateTime.UtcNow,
                IsPublished = false,
                Stages =
                {
                    new FormStage { Order = 1, Title = "بيانات الموظف", Role = StageRole.Employee },
                    new FormStage { Order = 2, Title = "قرار المشرف", Role = StageRole.Supervisor }
                }
            };

            _db.FormTemplates.Add(template);
            await _db.SaveChangesAsync();

            return RedirectToPage("/Admin/Edit", new { id = template.Id });
        }

        public async Task<IActionResult> OnPostTogglePublishAsync(int id)
        {
            var template = await _db.FormTemplates.Include(t => t.Stages).ThenInclude(s => s.Fields)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (template == null)
            {
                return NotFound();
            }

            if (!template.IsPublished && !template.Stages.Any(s => s.Fields.Any()))
            {
                TempData["StatusMessage"] = "أضف أسئلة إلى المراحل قبل النشر.";
                return RedirectToPage();
            }

            template.IsPublished = !template.IsPublished;
            await _db.SaveChangesAsync();
            TempData["StatusMessage"] = template.IsPublished ? "تم نشر الاستمارة." : "تم إلغاء نشر الاستمارة.";
            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            Templates = await _db.FormTemplates
                .Include(t => t.Stages)
                .OrderByDescending(t => t.CreatedUtc)
                .ToListAsync();

            SubmissionCounts = await _db.Submissions
                .GroupBy(s => s.FormTemplateId)
                .Select(g => new { TemplateId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TemplateId, x => x.Count);
        }
    }
}
