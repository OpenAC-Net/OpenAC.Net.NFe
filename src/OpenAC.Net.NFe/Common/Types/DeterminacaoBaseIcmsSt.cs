using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum DeterminacaoBaseIcmsSt
{
    [DFeEnum("0")]
    PrecoTabelado = 0,
    [DFeEnum("1")]
    ListaNegativa = 1,
    [DFeEnum("2")]
    ListaPositiva = 2,
    [DFeEnum("3")]
    ListaNeutra = 3,
    [DFeEnum("4")]
    MargemValorAgregado = 4,
    [DFeEnum("5")]
    Pauta = 5,
    [DFeEnum("6")]
    Codigo6 = 6,
}