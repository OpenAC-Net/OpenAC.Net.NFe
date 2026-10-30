using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum CondicaoVeiculo
{
    [DFeEnum("1")]
    Acabado = 1,
    [DFeEnum("2")]
    Inacabado = 2,
    [DFeEnum("3")]
    Semiacabado = 3,
}