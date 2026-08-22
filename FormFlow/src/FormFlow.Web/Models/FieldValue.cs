using System;

namespace FormFlow.Web.Models
{
    public class FieldValue
    {
        public int Id { get; set; }

        public int SubmissionId { get; set; }

        public Submission Submission { get; set; }

        public int FormFieldId { get; set; }

        public FormField FormField { get; set; }

        /// <summary>Answer text. Multi choice answers are stored as " | " separated values.</summary>
        public string Value { get; set; }

        public DateTime UpdatedUtc { get; set; }

        public const string MultiValueSeparator = " | ";
    }
}
