using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum OrigemMercadoria
{
    [DFeEnum("0")]
    Nacional = 0,
    [DFeEnum("1")]
    EstrangeiraImportacaoDireta = 1,
    [DFeEnum("2")]
    EstrangeiraAdquiridaBrasil = 2,
    [DFeEnum("3")]
    NacionalConteudoImportacaoSuperior40 = 3,
    [DFeEnum("4")]
    NacionalProcessosBasicos = 4,
    [DFeEnum("5")]
    NacionalConteudoImportacaoInferiorIgual40 = 5,
    [DFeEnum("6")]
    EstrangeiraImportacaoDiretaSemSimilar = 6,
    [DFeEnum("7")]
    EstrangeiraAdquiridaBrasilSemSimilar = 7,
    [DFeEnum("8")]
    NacionalConteudoImportacaoSuperior70 = 8,
}