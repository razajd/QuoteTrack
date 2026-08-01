using System;
using System.Threading.Tasks;
using QuoteTrack.Application.DTOs;

namespace QuoteTrack.Application.Interfaces
{
    public interface ICorporateReportService
    {
        Task<CorporateReportDto> GenerateAsync(
            string reportCode,
            string? userId,
            DateTime dateFrom,
            DateTime dateTo,
            string grouping);
    }
}
