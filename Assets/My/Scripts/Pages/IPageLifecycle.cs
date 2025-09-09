using System.Threading;
using System.Threading.Tasks;

public interface IPageLifecycle
{
    Task InitializeAsync(CancellationToken token);
    Task EnterAsync(CancellationToken token);
    Task ExitAsync(CancellationToken token);
    Task DisposeAsync();
}