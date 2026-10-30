using OpenAC.Net.NFe.Common.Model;

namespace OpenAC.Net.NFe.DANFe.PDFSharp.Web;

public interface IOpenDANFeWeb
{
    ValueTask<byte[]> GerarPDFAsync(NFeProc nota, string empresa = "default", CancellationToken cancellationToken = default);
    ValueTask<byte[]> GerarPDFAsync(IEnumerable<NFeProc> notas, string empresa = "default", CancellationToken cancellationToken = default);
    ValueTask<byte[]> GerarEventosPDFAsync(IEnumerable<NFeProcEvento> eventos, string empresa = "default", CancellationToken cancellationToken = default);
    ValueTask<byte[]> GerarInutilizacaoPDFAsync(NFeProcInutilizacao inutilizacao, string empresa = "default", CancellationToken cancellationToken = default);
}
