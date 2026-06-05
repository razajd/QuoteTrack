using System.Threading.Tasks;

namespace QuoteTrack.Application.Interfaces
{
    public interface IQuoteListReadModelService
    {
        Task RefreshAllAsync();
        Task MarkStaleAsync();
    }
}
