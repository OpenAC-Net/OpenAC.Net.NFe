using OpenAC.Net.DFe.Core.Attributes;

namespace OpenAC.Net.NFe.Common.Types;

public enum TipoRestricao
{
    [DFeEnum("0")]
    Nenhuma = 0,
    [DFeEnum("1")]
    AlienacaoFiduciaria = 1,
    [DFeEnum("2")]
    ArrendamentoMercantil = 2,
    [DFeEnum("3")]
    ReservaDeDominio = 3,
    [DFeEnum("4")]
    PenhorDeVeiculos = 4,
    [DFeEnum("9")]
    Outras = 9,
}