using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum TipoIntegracaoPagamento
{
    [DFeEnum("1")]
    TipIntegradoAutomacao = 1,
    [DFeEnum("2")]
    TipNaoIntegrado = 2,
}