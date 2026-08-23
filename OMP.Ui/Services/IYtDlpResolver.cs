using System.Threading;
using System.Threading.Tasks;

namespace OMP.Ui.Services;

public interface IYtDlpResolver
{
    public Task<YtDlpResolveResult> ResolveAsync(string pageUrl, CancellationToken cancellationToken);
}
