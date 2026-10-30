using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum IndOperacao
{
    [DFeEnum("1")]
    Interna = 1,
    [DFeEnum("2")]
    Interestadual = 2,
    [DFeEnum("3")]
    Exterior = 3,
}