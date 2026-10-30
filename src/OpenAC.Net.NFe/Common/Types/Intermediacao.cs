using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum Intermediacao
{
    [DFeEnum("1")]
    ContaPropria = 1,
    [DFeEnum("2")]
    ContaeOrdem = 2,
    [DFeEnum("3")]
    PorEncomenda = 3,
}