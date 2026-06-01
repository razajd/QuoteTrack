// QuoteTrack.Application/Interfaces/IPdfQuotationExtractor.cs
using System.Threading.Tasks;
using QuoteTrack.Domain.Entities;

namespace QuoteTrack.Application.Interfaces
{
    public interface IPdfQuotationExtractor
    {
        Task ExtractAsync(string filePath, Quote quote);
    }
}