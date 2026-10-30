using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Services.Recepcao;

public class NFeAutorizacaoServiceClient(NFeConfig config, X509Certificate2 certificado)
    : NFeServiceClient<NFeAutorizacaoResult, NFeAutorizacaoResposta>(config, TipoServicoNFe.NFeAutorizacao, SchemaNFe.EnviNFe, "retEnviNFe", certificado)
{
    public Task<NFeAutorizacaoResposta> AutorizarAsync(string lote, bool sincrono, IEnumerable<Common.Model.NFe> documentos,
        CancellationToken token = default) => EnviarAsync(new NFeEnvio
        { Lote = lote, IndicadorSincrono = sincrono ? "1" : "0", Documentos = new(documentos) }, token);

    protected override NFeAutorizacaoResposta CriarResposta(string xmlEnvio, string xmlRetorno) =>
        new(xmlEnvio, xmlRetorno, EnvelopeEnvio, EnvelopeRetorno);
}
