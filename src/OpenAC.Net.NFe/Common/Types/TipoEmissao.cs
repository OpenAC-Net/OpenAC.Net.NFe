using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum TipoEmissao
{
    [DFeEnum("1")]
    Normal = 1,
    [DFeEnum("2")]
    FSIA = 2,
    [DFeEnum("3")]
    SCAN = 3,
    [DFeEnum("4")]
    EPEC = 4,
    [DFeEnum("5")]
    FSDA = 5,
    [DFeEnum("6")]
    SVCAN = 6,
    [DFeEnum("7")]
    SVCRS = 7,
    [DFeEnum("9")]
    OffLine = 9,
}