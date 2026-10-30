using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum TipoNFe
{
    [DFeEnum("0")]
    Entrada = 0,
    [DFeEnum("1")]
    Saida = 1,
}