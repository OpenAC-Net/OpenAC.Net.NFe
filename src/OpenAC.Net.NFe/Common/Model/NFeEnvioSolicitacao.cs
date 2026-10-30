using System.Collections.Generic;

namespace OpenAC.Net.NFe;

public sealed class NFeEnvioSolicitacao
{
    public string Lote { get; set; } = "1";
    public bool Sincrono { get; set; } = true;
    public IEnumerable<Common.Model.NFe> Documentos { get; set; } = [];
}
