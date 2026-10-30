using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum CRT
{
    [DFeEnum("1")]
    SimplesNacional = 1,
    [DFeEnum("2")]
    SimplesNacionalExcessoSublimite = 2,
    [DFeEnum("3")]
    RegimeNormal = 3,
    [DFeEnum("4")]
    Codigo4 = 4,
}