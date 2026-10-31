using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.DFe.Core.Http;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.DFe.Core.Document;
using OpenAC.Net.DFe.Core.Service;
using OpenAC.Net.DFe.Core.Storage;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Services;

/// <summary>Base dos serviços NF-e: o Core monta o SOAP, envia HTTP, aplica cancelamento e trata falhas.</summary>
public abstract class NFeServiceClient<TResult, TResposta> : DFeSoapServiceClient<NFeConfig, NFeConfigGeral, NFeConfigWebServices,
    NFeConfigCertificados, NFeConfigArquivos>
    where TResult : NFeResultBase<TResult>
    where TResposta : NFeResposta<TResult>
{
    private readonly X509Certificate2 certificado;
    private readonly SemaphoreSlim gate = new(1, 1);

    protected NFeServiceClient(NFeConfig config, TipoServicoNFe servico, SchemaNFe schema, string retorno,
        X509Certificate2 certificado)
        : base(config, config.ObterEndereco(servico), SoapVersion.Soap12)
    {
        this.certificado = certificado ?? throw new ArgumentNullException(nameof(certificado));
        Servico = servico;
        Schema = schema;
        ElementoRetorno = retorno;
        NomeArquivo = servico.ToString();
    }

    protected TipoServicoNFe Servico { get; }

    protected string ElementoRetorno { get; }

    protected SchemaNFe Schema { get; }

    protected override X509Certificate2 Certificado => certificado;

    protected override TimeSpan TimeoutOperacao => Configuracoes.WebServices.TimeoutOperacao;

    protected override bool EncapsularExcecoesComunicacao => false;

    protected override bool AbreviarCabecalhoSoapVazio => false;

    protected override DFeHttpTransportOptions CriarOpcoesHttp() => new(Configuracoes.WebServices, Certificado,
        ValidarCertificadoServidor(), TimeoutOperacao)
    {
        UseCookies = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        MargemValidadeCertificado = Configuracoes.Certificados.MargemValidadeCertificado,
    };

    protected override string CriarNomeArquivo(bool resposta) =>
        $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}_{NomeArquivo}_{(resposta ? "retorno" : "envio")}.xml";

    /// <summary>Envia o payload fiscal validado no contrato SOAP deste serviço.</summary>
    protected async Task<TResposta> EnviarAsync<TDocumento>(DFeDocument<TDocumento> dados, CancellationToken token)
        where TDocumento : class
    {
        token.ThrowIfCancellationRequested();
        var xml = dados.GetXml(DFeSaveOptions.DisableFormatting | DFeSaveOptions.OmitDeclaration);
        ValidateMessage(xml, Configuracoes.Arquivos.GetSchema(Schema));
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var action = ObterAcao();
            var serviceNs = action.Substring(0, action.LastIndexOf('/'));
            var payload = new StringBuilder();
            if (Servico == TipoServicoNFe.NFeDistribuicaoDFe)
                payload.Append($"<nfeDistDFeInteresse xmlns=\"{serviceNs}\">");
            payload.Append($"<nfeDadosMsg xmlns=\"{serviceNs}\">");
            payload.Append(xml);
            payload.Append("</nfeDadosMsg>");
            if (Servico == TipoServicoNFe.NFeDistribuicaoDFe)
                payload.Append("</nfeDistDFeInteresse>");
            var result = await ExecuteAsync(action, payload.ToString(), "", token).ConfigureAwait(false);
            return CriarResposta(xml, result);
        }
        finally
        {
            gate.Release();
        }
    }

    protected virtual string ObterAcao()
    {
        const string prefix = "http://www.portalfiscal.inf.br/nfe/wsdl/";
        switch (Servico)
        {
            case TipoServicoNFe.NFeDistribuicaoDFe:
                return prefix + "NFeDistribuicaoDFe/nfeDistDFeInteresse";
            case TipoServicoNFe.NfeConsultaCadastro:
                return prefix + "CadConsultaCadastro4/consultaCadastro";
            case TipoServicoNFe.RecepcaoEventoAN:
                return prefix + "NFeRecepcaoEvento4/nfeRecepcaoEvento";
        }

        var normal = NFeServiceManager.Perfil(Configuracoes, Servico) ==
                     DFeTipoEmissao.Normal;
        var sp = normal && Configuracoes.WebServices.UF == DFeSiglaUF.SP;
        var ba = normal && Configuracoes.Geral.Modelo == ModeloNFe.NFe &&
                 Configuracoes.WebServices.UF == DFeSiglaUF.BA;
        var contract = Servico switch
        {
            TipoServicoNFe.NFeAutorizacao => (sp ? "NfeAutorizacao4" : "NFeAutorizacao4") + "/nfeAutorizacaoLote",
            TipoServicoNFe.NFeRetAutorizacao => (sp ? "NfeRetAutorizacao4" : "NFeRetAutorizacao4") +
                                                "/nfeRetAutorizacaoLote",
            TipoServicoNFe.NFeInutilizacao => (sp ? "NfeInutilizacao4" : "NFeInutilizacao4") + "/nfeInutilizacaoNF",
            TipoServicoNFe.NFeConsultaProtocolo =>
                (sp ? "NfeConsulta4" : ba ? "NFeConsulta4" : "NFeConsultaProtocolo4") + "/nfeConsultaNF",
            TipoServicoNFe.NFeStatusServico => (sp ? "NfeStatusServico4" : "NFeStatusServico4") + "/nfeStatusServicoNF",
            TipoServicoNFe.RecepcaoEvento => sp
                ? "RecepcaoEvento4/nfeRecepcaoEventoNF"
                : "NFeRecepcaoEvento4/" + (ba ? "nfeRecepcaoEventoNF" : "nfeRecepcaoEvento"),
            _ => throw new NotSupportedException("Contrato SOAP não definido para " + Servico)
        };
        return prefix + contract;
    }

    protected override string TratarRetorno(XElement body)
    {
        var result = body.DescendantsAndSelf(XName.Get(ElementoRetorno, NFeConst.Namespace)).SingleOrDefault();
        return result == null
            ? throw new OpenDFeCommunicationException("Resposta SEFAZ não contém " + ElementoRetorno + ".")
            : result.ToString(SaveOptions.DisableFormatting);
    }

    protected override Task GravarSoapAsync(string content, string name, CancellationToken token) =>
        !Configuracoes.WebServices.Salvar
            ? Task.CompletedTask
            : Configuracoes.DocumentStore.SaveAsync(
                new DFeDocument(Path.GetFullPath(Path.Combine(Configuracoes.Arquivos.PathSalvar, name)), content,
                    DFeArquivoExiste.Sobrescrever), token);

    protected override void DisposeManaged()
    {
        gate.Dispose();
        base.DisposeManaged();
    }

    protected string Ambiente => ((int)Configuracoes.WebServices.Ambiente).ToString(System.Globalization.CultureInfo.InvariantCulture);
    protected abstract TResposta CriarResposta(string xmlEnvio, string xmlRetorno);
}
