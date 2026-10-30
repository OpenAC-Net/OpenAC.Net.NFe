using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum FormaPagamento
{
    [DFeEnum("01")]
    fpDinheiro = 1,
    [DFeEnum("02")]
    fpCheque = 2,
    [DFeEnum("03")]
    fpCartaoCredito = 3,
    [DFeEnum("04")]
    fpCartaoDebito = 4,
    [DFeEnum("05")]
    fpCreditoLoja = 5,
    [DFeEnum("10")]
    fpValeAlimentacao = 10,
    [DFeEnum("11")]
    fpValeRefeicao = 11,
    [DFeEnum("12")]
    fpValePresente = 12,
    [DFeEnum("13")]
    fpValeCombustivel = 13,
    [DFeEnum("99")]
    fpOutro = 99,
}