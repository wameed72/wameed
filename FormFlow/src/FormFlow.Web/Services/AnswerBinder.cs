using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FormFlow.Web.Models;
using Microsoft.AspNetCore.Http;

namespace FormFlow.Web.Services
{
    /// <summary>Reads the posted answers of a stage. Inputs are named <c>field_{fieldId}</c>.</summary>
    public static class AnswerBinder
    {
        public static string InputName(FormField field) => "field_" + field.Id.ToString(CultureInfo.InvariantCulture);

        public static Dictionary<int, string> FromForm(IFormCollection form, FormStage stage)
        {
            var answers = new Dictionary<int, string>();
            foreach (var field in stage.Fields)
            {
                var values = form[InputName(field)]
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim())
                    .ToList();

                answers[field.Id] = field.Type == FieldType.Checkbox
                    ? string.Join(FieldValue.MultiValueSeparator, values)
                    : values.FirstOrDefault() ?? string.Empty;
            }

            return answers;
        }
    }
}
