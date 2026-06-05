using System.Threading.Tasks;
using QuoteTrack.Application.DTOs;

namespace QuoteTrack.Application.Interfaces
{
    public interface IManagementDashboardService
    {
        Task<ManagementDashboardDto> GetOverviewAsync();
    }
}
