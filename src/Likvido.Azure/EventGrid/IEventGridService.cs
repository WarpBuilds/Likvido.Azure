using System.Threading.Tasks;

namespace Likvido.Azure.EventGrid
{
    public interface IEventGridService
    {
        Task PublishAsync(params IEvent[] events);
    }
}
