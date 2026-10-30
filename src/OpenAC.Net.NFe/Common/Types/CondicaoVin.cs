using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum CondicaoVin
{
    [DFeEnum("R")]
    Remarcado,
    [DFeEnum("N")]
    Normal,
}