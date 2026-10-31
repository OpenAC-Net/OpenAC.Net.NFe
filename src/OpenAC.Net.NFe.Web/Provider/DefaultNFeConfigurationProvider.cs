using OpenAC.Net.NFe.Common;

namespace OpenAC.Net.NFe.Web;

internal sealed class DefaultNFeConfigurationProvider(Action<NFeConfig> configure)
    : INFeConfigurationProvider<NFeConfig>
{
    public ValueTask<NFeConfig> GetConfigurationAsync(string tenantId = "default",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var config = new NFeConfig();
        configure(config);
        return new ValueTask<NFeConfig>(config);
    }
}
