using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FormFlow.Web.Models
{
    public class Submission
    {
        public int Id { get; set; }

        public int FormTemplateId { get; set; }

        public FormTemplate FormTemplate { get; set; }

        /// <summary>Short human readable code shown to the employee so the case can be tracked.</summary>
        [Required]
        [StringLength(16)]
        public string TrackingCode { get; set; }

        /// <summary>Order of the stage that is waiting to be filled.</summary>
        public int CurrentStageOrder { get; set; }

        public SubmissionStatus Status { get; set; }

        [StringLength(200)]
        public string SubmitterName { get; set; }

        [StringLength(200)]
        public string SubmitterEmail { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime UpdatedUtc { get; set; }

        public List<FieldValue> Values { get; set; } = new List<FieldValue>();

        public List<SubmissionEvent> Events { get; set; } = new List<SubmissionEvent>();
    }
}
