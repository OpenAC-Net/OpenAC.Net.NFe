using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;

namespace OpenAC.Net.NFe.DANFe.PDFSharp.Web;

internal sealed class DANFeConfigurationProvider(Action<DANFeConfig> configurar) : IDANFeConfigurationProvider
{
    public ValueTask<DANFeConfig> GetAsync(string empresa, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configuracao = new DANFeConfig();
        configurar(configuracao);
        return ValueTask.FromResult(configuracao);
    }
}
