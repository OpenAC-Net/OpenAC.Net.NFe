using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Services.RecepcaoEvento;

public class NFeRecepcaoEventoServiceClient(NFeConfig config, X509Certificate2 certificado, bool nacional = false)
    : NFeServiceClient<NFeEventoResult, NFeEventoResposta>(config, nacional ? TipoServicoNFe.RecepcaoEventoAN : TipoServicoNFe.RecepcaoEvento, SchemaNFe.EnvEvento, "retEnvEvento", certificado)
{
    public Task<NFeEventoResposta> ReceberAsync(string lote, IEnumerable<NFeEvento> eventosAssinados,
        CancellationToken token = default) => EnviarAsync(new NFeEnvioEvento
        { Lote = lote, Eventos = new(eventosAssinados) }, token);

    protected override NFeEventoResposta CriarResposta(string xmlEnvio, string xmlRetorno) =>
        new(xmlEnvio, xmlRetorno, EnvelopeEnvio, EnvelopeRetorno);
}
