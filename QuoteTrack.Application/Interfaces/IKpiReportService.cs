using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuoteTrack.Application.DTOs;
using QuoteTrack.Domain.Entities;

namespace QuoteTrack.Application.Interfaces
{
    public interface IKpiReportService
    {
        Task<List<ApplicationUser>> GetKpiUsersAsync();
        Task<KpiReportDto> GetReportAsync(string employeeId, string templateCode, string reportType, DateTime periodAnchor);
        Task SaveReportAsync(KpiReportDto report, string? submittedByUserId);
    }
}
