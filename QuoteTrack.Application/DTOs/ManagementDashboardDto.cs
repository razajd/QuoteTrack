using System.Collections.Generic;

namespace QuoteTrack.Application.DTOs
{
    public class ManagementDashboardDto
    {
        public decimal TotalPipelineValue { get; set; }
        public double DepartmentWinRate { get; set; }
        public int UnassignedCount { get; set; }
        public int OverdueTasksCount { get; set; }
        public int LeadDueCount { get; set; }
        public int LeadOverdueCount { get; set; }
        public int QuoteDueCount { get; set; }
        public int QuoteOverdueCount { get; set; }
        public int PendingLeadClosureCount { get; set; }
        public List<ManagementRepStatDto> TeamStats { get; set; } = new();
    }

    public class ManagementRepStatDto
    {
        public string UserId { get; set; } = string.Empty;
        public string RepName { get; set; } = string.Empty;
        public int ActiveDeals { get; set; }
        public int WonDeals { get; set; }
        public double WinRate { get; set; }
        public decimal PipelineValue { get; set; }
        public int OverdueTasks { get; set; }
        public int LeadsDue { get; set; }
        public int QuotesDue { get; set; }
        public int StaleDeals { get; set; }
    }
}
