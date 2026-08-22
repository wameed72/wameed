using System.Collections.Generic;
using System.Linq;
using System.Text;
using FormFlow.Web.Models;

namespace FormFlow.Web.Services
{
    public static class CsvExporter
    {
        /// <summary>One row per submission, one column per field of every stage.</summary>
        public static string BuildCsv(FormTemplate template, IEnumerable<Submission> submissions)
        {
            var fields = template.Stages
                .OrderBy(s => s.Order)
                .SelectMany(s => s.Fields.OrderBy(f => f.Order).Select(f => new { Stage = s, Field = f }))
                .ToList();

            var builder = new StringBuilder();
            var header = new List<string> { "رمز المتابعة", "الحالة", "المرحلة الحالية", "تاريخ الإنشاء", "آخر تحديث" };
            header.AddRange(fields.Select(f => $"{f.Stage.Title} - {f.Field.Label}"));
            builder.AppendLine(string.Join(",", header.Select(Escape)));

            foreach (var submission in submissions)
            {
                var currentStage = template.Stages.FirstOrDefault(s => s.Order == submission.CurrentStageOrder);
                var row = new List<string>
                {
                    submission.TrackingCode,
                    DisplayNames.Status(submission.Status),
                    submission.Status == SubmissionStatus.Completed ? "-" : currentStage?.Title ?? "-",
                    submission.CreatedUtc.ToString("yyyy-MM-dd HH:mm"),
                    submission.UpdatedUtc.ToString("yyyy-MM-dd HH:mm")
                };

                row.AddRange(fields.Select(f =>
                    submission.Values.FirstOrDefault(v => v.FormFieldId == f.Field.Id)?.Value ?? string.Empty));

                builder.AppendLine(string.Join(",", row.Select(Escape)));
            }

            return builder.ToString();
        }

        private static string Escape(string value)
        {
            value ??= string.Empty;
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
