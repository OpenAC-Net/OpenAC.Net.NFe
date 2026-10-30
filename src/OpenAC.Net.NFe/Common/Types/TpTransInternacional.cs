using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum TpTransInternacional
{
    [DFeEnum("1")]
    Maritima = 1,
    [DFeEnum("2")]
    Fluvial = 2,
    [DFeEnum("3")]
    Lacustre = 3,
    [DFeEnum("4")]
    Aerea = 4,
    [DFeEnum("5")]
    Postal = 5,
    [DFeEnum("6")]
    Ferroviaria = 6,
    [DFeEnum("7")]
    Rodoviaria = 7,
    [DFeEnum("8")]
    CondutoRedeTransmissão = 8,
    [DFeEnum("9")]
    MeiosProprios = 9,
    [DFeEnum("10")]
    EntradaSaidaficta = 10,
    [DFeEnum("11")]
    Courier = 11,
    [DFeEnum("12")]
    Handcarry = 12,
    [DFeEnum("13")]
    Codigo13 = 13,
}