using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum IndIEDest
{
    [DFeEnum("1")]
    ContribuinteICMS = 1,
    [DFeEnum("2")]
    Isento = 2,
    [DFeEnum("9")]
    NaoContribuinte = 9,
}