namespace OpenAC.Net.NFe;

public sealed class NFeInutilizacaoSolicitacao
{
    public string Cnpj { get; set; }
    public int Ano { get; set; }
    public int Serie { get; set; }
    public int NumeroInicial { get; set; }
    public int NumeroFinal { get; set; }
    public string Justificativa { get; set; }
}
