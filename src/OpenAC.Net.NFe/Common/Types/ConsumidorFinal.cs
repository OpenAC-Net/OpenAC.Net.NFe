using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum ConsumidorFinal
{
    [DFeEnum("0")]
    Nao = 0,
    [DFeEnum("1")]
    Sim = 1,
}