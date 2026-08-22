using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FormFlow.Web.Data;
using FormFlow.Web.Models;
using FormFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FormFlow.Web.Pages.Admin
{
    public class SubmissionsModel : PageModel
    {
        private readonly FormFlowDbContext _db;

        public SubmissionsModel(FormFlowDbContext db)
        {
            _db = db;
        }

        public FormTemplate Template { get; private set; }

        public List<Submission> Submissions { get; private set; } = new List<Submission>();

        public async Task<IActionResult> OnGetAsync(int templateId)
        {
            if (!await LoadAsync(templateId))
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnGetExportAsync(int templateId)
        {
            if (!await LoadAsync(templateId))
            {
                return NotFound();
            }

            var csv = CsvExporter.BuildCsv(Template, Submissions);

            // BOM so Excel opens the Arabic text with the right encoding.
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return File(bytes, "text/csv", $"submissions-{templateId}.csv");
        }

        private async Task<bool> LoadAsync(int templateId)
        {
            Template = await _db.FormTemplates
                .Include(t => t.Stages)
                .ThenInclude(s => s.Fields)
                .FirstOrDefaultAsync(t => t.Id == templateId);

            if (Template == null)
            {
                return false;
            }

            Template.Stages = Template.Stages.OrderBy(s => s.Order).ToList();

            Submissions = await _db.Submissions
                .Include(s => s.Values)
                .Where(s => s.FormTemplateId == templateId)
                .OrderByDescending(s => s.CreatedUtc)
                .ToListAsync();

            return true;
        }
    }
}
