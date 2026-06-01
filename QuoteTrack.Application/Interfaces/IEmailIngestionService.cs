// QuoteTrack.Application/Interfaces/IEmailIngestionService.cs
using System.Threading;
using System.Threading.Tasks;

namespace QuoteTrack.Application.Interfaces
{
    public interface IEmailIngestionService
    {
        Task ProcessNewEmailsAsync(CancellationToken cancellationToken);
    }
}