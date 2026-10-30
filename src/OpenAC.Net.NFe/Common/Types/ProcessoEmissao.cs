using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum ProcessoEmissao
{
    [DFeEnum("0")]
    AplicativoContribuinte = 0,
    [DFeEnum("1")]
    AvulsaFisco = 1,
    [DFeEnum("2")]
    AvulsaContribuinte = 2,
    [DFeEnum("3")]
    ContribuinteAplicativoFisco = 3,
    [DFeEnum("4")]
    Codigo4 = 4,
}