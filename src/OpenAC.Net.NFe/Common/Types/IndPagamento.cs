using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum IndPagamento
{
    [DFeEnum("0")]
    AVista = 0,
    [DFeEnum("1")]
    APrazo = 1,
    [DFeEnum("2")]
    Outras = 2,
}