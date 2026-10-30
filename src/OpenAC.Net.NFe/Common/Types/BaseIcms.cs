using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum BaseIcms
{
    [DFeEnum("0")]
    MargemValorAgregado = 0,
    [DFeEnum("1")]
    Pauta = 1,
    [DFeEnum("2")]
    PrecoTabelado = 2,
    [DFeEnum("3")]
    ValorOperacao = 3,
}