using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum TipoImpressao
{
    [DFeEnum("0")]
    SemGeracao = 0,
    [DFeEnum("1")]
    Retrato = 1,
    [DFeEnum("2")]
    Paisagem = 2,
    [DFeEnum("3")]
    Simplificado = 3,
    [DFeEnum("4")]
    NFCe = 4,
    [DFeEnum("5")]
    MsgEletronica = 5,
    [DFeEnum("6")]
    Codigo6 = 6,
}