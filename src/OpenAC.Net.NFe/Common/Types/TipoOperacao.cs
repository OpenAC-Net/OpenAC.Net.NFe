using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum TipoOperacao
{
    [DFeEnum("0")]
    Outros = 0,
    [DFeEnum("1")]
    VendaConcessionaria = 1,
    [DFeEnum("2")]
    FaturaConsumidorFinal = 2,
    [DFeEnum("3")]
    VendaGrandesConsumidores = 3,
}