// QuoteTrack.Application/Interfaces/IExcelQuotationExtractor.cs
using System.Threading.Tasks;
using QuoteTrack.Domain.Entities;

namespace QuoteTrack.Application.Interfaces
{
    public interface IExcelQuotationExtractor
    {
        Task ExtractAsync(string filePath, Quote quote);
    }
}