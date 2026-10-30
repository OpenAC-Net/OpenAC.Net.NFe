using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum FinalidadeNFe
{
    [DFeEnum("1")]
    Normal = 1,
    [DFeEnum("2")]
    Complementar = 2,
    [DFeEnum("3")]
    Ajuste = 3,
    [DFeEnum("4")]
    Devolucao = 4,
    [DFeEnum("5")]
    Codigo5 = 5,
    [DFeEnum("6")]
    Codigo6 = 6,
}