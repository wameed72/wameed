using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FormFlow.Web.Data;
using FormFlow.Web.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FormFlow.Web.Pages
{
    public class IndexModel : PageModel
    {
        private readonly FormFlowDbContext _db;

        public IndexModel(FormFlowDbContext db)
        {
            _db = db;
        }

        public List<FormTemplate> PublishedTemplates { get; private set; } = new List<FormTemplate>();

        public async Task OnGetAsync()
        {
            PublishedTemplates = await _db.FormTemplates
                .Where(t => t.IsPublished)
                .OrderBy(t => t.Title)
                .ToListAsync();
        }
    }
}
