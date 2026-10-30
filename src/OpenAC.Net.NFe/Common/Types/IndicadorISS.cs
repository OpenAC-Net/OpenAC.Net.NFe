using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum IndicadorISS
{
    [DFeEnum("1")]
    iiExigivel = 1,
    [DFeEnum("2")]
    iiNaoIncidencia = 2,
    [DFeEnum("3")]
    iiIsencao = 3,
    [DFeEnum("4")]
    iiExportacao = 4,
    [DFeEnum("5")]
    iiImunidade = 5,
    [DFeEnum("6")]
    iiExigSuspDecisaoJudicial = 6,
    [DFeEnum("7")]
    iiExigSuspProcessoAdm = 7,
}