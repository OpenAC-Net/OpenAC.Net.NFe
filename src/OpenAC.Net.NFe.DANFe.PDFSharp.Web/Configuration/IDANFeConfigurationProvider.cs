using OpenAC.Net.NFe.DANFe.PDFSharp.Configuracao;

namespace OpenAC.Net.NFe.DANFe.PDFSharp.Web;

/// <summary>Obtém a configuração de impressão específica da empresa.</summary>
public interface IDANFeConfigurationProvider
{
    ValueTask<DANFeConfig> GetAsync(string empresa, CancellationToken cancellationToken = default);
}
