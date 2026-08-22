using System.Collections.Generic;
using FormFlow.Web.Models;

namespace FormFlow.Web.Pages.Shared
{
    public class StageAnswersModel
    {
        public FormStage Stage { get; set; }

        public IReadOnlyList<FieldValue> Values { get; set; } = new List<FieldValue>();
    }
}
