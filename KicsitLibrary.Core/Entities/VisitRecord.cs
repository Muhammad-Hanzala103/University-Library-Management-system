using System;
using System.Collections.Generic;

namespace KicsitLibrary.Core.Entities
{
    public class VisitRecord : EntityBase
    {
        public string VisitNumber { get; set; } = string.Empty;
        public string OrganizationName { get; set; } = string.Empty;
        public string VisitType { get; set; } = "ExternalVisit";
        public DateTime VisitDate { get; set; }
        public string VisitTeamMembers { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string Observations { get; set; } = string.Empty;
        public string Findings { get; set; } = string.Empty;
        public string Suggestions { get; set; } = string.Empty;
        public string Requirements { get; set; } = string.Empty;
        public string ActionTaken { get; set; } = string.Empty;
        public DateTime? NextFollowUpDate { get; set; }
        
        public int CreatedByUserId { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;
        
        public int? UpdatedByUserId { get; set; }
        public virtual User? UpdatedByUser { get; set; }

        public virtual ICollection<VisitFile> VisitFiles { get; set; } = new List<VisitFile>();
    }
}
