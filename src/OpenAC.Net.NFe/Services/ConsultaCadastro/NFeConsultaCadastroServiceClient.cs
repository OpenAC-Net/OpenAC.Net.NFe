using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Services.ConsultaCadastro;

public class NFeConsultaCadastroServiceClient(NFeConfig config, X509Certificate2 certificado)
    : NFeServiceClient<NFeCadastroResult, NFeCadastroResposta>(config, TipoServicoNFe.NfeConsultaCadastro, SchemaNFe.ConsCad, "retConsCad", certificado)
{
    public Task<NFeCadastroResposta> ConsultarAsync(string documento, string tipo, CancellationToken token = default) =>
        EnviarAsync(new NFeConsultaCadastro
        {
            InfCons = new NFeConsultaCadastroInfo
            {
                UF = Configuracoes.WebServices.UF.ToString(), IE = tipo == "IE" ? documento : null,
                CNPJ = tipo == "CNPJ" ? documento : null, CPF = tipo == "CPF" ? documento : null
            }
        }, token);

    protected override NFeCadastroResposta CriarResposta(string xmlEnvio, string xmlRetorno) =>
        new(xmlEnvio, xmlRetorno, EnvelopeEnvio, EnvelopeRetorno);
}
