using System;

namespace FormFlow.Web.Models
{
    public class SubmissionEvent
    {
        public int Id { get; set; }

        public int SubmissionId { get; set; }

        public Submission Submission { get; set; }

        public int? FormStageId { get; set; }

        public string Actor { get; set; }

        public string Action { get; set; }

        public string Note { get; set; }

        public DateTime CreatedUtc { get; set; }
    }
}
