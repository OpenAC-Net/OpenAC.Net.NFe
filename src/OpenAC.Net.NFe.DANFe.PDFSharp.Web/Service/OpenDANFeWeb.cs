using OpenAC.Net.NFe.Common.Model;

namespace OpenAC.Net.NFe.DANFe.PDFSharp.Web;

internal sealed class OpenDANFeWeb(IDANFeConfigurationProvider configuracoes) : IOpenDANFeWeb
{
    public ValueTask<byte[]> GerarPDFAsync(NFeProc nota, string empresa = "default", CancellationToken cancellationToken = default) =>
        GerarPDFAsync(new[] { nota }, empresa, cancellationToken);

    public async ValueTask<byte[]> GerarPDFAsync(IEnumerable<NFeProc> notas, string empresa = "default", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var documentos = notas?.ToArray() ?? throw new ArgumentNullException(nameof(notas));
        var gerador = await CriarAsync(empresa, cancellationToken).ConfigureAwait(false);
        return gerador.GerarPDF(cancellationToken, documentos);
    }
    public async ValueTask<byte[]> GerarEventosPDFAsync(IEnumerable<NFeProcEvento> eventos, string empresa = "default", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var documentos = eventos?.ToArray() ?? throw new ArgumentNullException(nameof(eventos));
        var gerador = await CriarAsync(empresa, cancellationToken).ConfigureAwait(false);
        return gerador.GerarEventoPDF(cancellationToken, documentos);
    }
    public async ValueTask<byte[]> GerarInutilizacaoPDFAsync(NFeProcInutilizacao inutilizacao, string empresa = "default", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var gerador = await CriarAsync(empresa, cancellationToken).ConfigureAwait(false);
        return gerador.GerarInutilizacaoPDF(inutilizacao, cancellationToken);
    }
    private async ValueTask<OpenDANFe> CriarAsync(string empresa, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(empresa)) throw new ArgumentException("Empresa não informada.", nameof(empresa));
        cancellationToken.ThrowIfCancellationRequested();
        var origem = await configuracoes.GetAsync(empresa, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Configuração do DANFE não informada.");
        cancellationToken.ThrowIfCancellationRequested();
        return new OpenDANFe(origem);
    }
}
