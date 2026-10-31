using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.DFe.Core.Extensions;
using OpenAC.Net.DFe.Core.Storage;
using OpenAC.Net.NFe.Certificates;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.Services;
using OpenAC.Net.NFe.Services.Consulta;
using OpenAC.Net.NFe.Services.ConsultaCadastro;
using OpenAC.Net.NFe.Services.DistribuicaoDFe;
using OpenAC.Net.NFe.Services.Inutilizacao;
using OpenAC.Net.NFe.Services.Recepcao;
using OpenAC.Net.NFe.Services.RecepcaoEvento;
using OpenAC.Net.NFe.Services.RetRecepcao;
using OpenAC.Net.NFe.Services.StatusServico;

namespace OpenAC.Net.NFe;

/// <summary>Cliente assíncrono com configuração isolada e recursos locais a cada operação.</summary>
public sealed class OpenNFe : IOpenNFeClient
{
    private readonly NFeConfig configuracao;
    private readonly INFeCertificadoProvider certificados;

    /// <summary>Captura a configuração. Transportes e stores injetados devem suportar chamadas concorrentes.</summary>
    public OpenNFe(NFeConfig configuracao, INFeCertificadoProvider certificados = null)
    {
        this.configuracao = (configuracao ?? throw new ArgumentNullException(nameof(configuracao))).Copiar();
        this.certificados = certificados ?? new NFeCertificadoProvider(this.configuracao.Certificado);
    }

    public Task<NFeAutorizacaoResposta> AutorizarAsync(Common.Model.NFe documento,
        CancellationToken cancellationToken = default)
    {
        ValidarOperacao(cancellationToken);
        if (documento == null) throw new ArgumentNullException(nameof(documento));
        return EnviarAsync(new NFeEnvioSolicitacao { Documentos = [documento] }, cancellationToken);
    }

    public async Task<NFeAutorizacaoResposta> EnviarAsync(NFeEnvioSolicitacao solicitacao,
        CancellationToken cancellationToken = default)
    {
        ValidarOperacao(cancellationToken);

        if (solicitacao == null)
            throw new ArgumentNullException(nameof(solicitacao));

        var lote = solicitacao.Lote;
        var sincrono = solicitacao.Sincrono;
        Numero(lote, 1, 15, nameof(solicitacao.Lote));
        var notas = CopiarDocumentos(solicitacao.Documentos);
        if (notas.Length < 1 || notas.Length > 50 || sincrono && notas.Length != 1)
            throw new ArgumentException("Lote deve conter 1 a 50 notas; envio síncrono exige uma nota.");
        if (configuracao.Geral.Modelo == ModeloNFe.NFCe && (!sincrono || notas.Length != 1))
            throw new ArgumentException("NFC-e deve ser enviada individualmente e de forma síncrona.");
        foreach (var nota in notas) ValidarDocumento(configuracao, nota);
        using var certificate = await ObterCertificadoAsync(cancellationToken).ConfigureAwait(false);
        return await NFeAutorizador.AutorizarAsync(configuracao, lote, sincrono, notas, certificate,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<NFeStatusServicoResposta> ConsultarSituacaoServicoAsync(
        CancellationToken cancellationToken = default)
    {
        ValidarOperacao(cancellationToken);
        using var certificate = await ObterCertificadoAsync(cancellationToken).ConfigureAwait(false);
        using var service = new NFeStatusServicoServiceClient(configuracao, certificate);
        return await service.ConsultarAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<NFeConsultaResposta> ConsultarAsync(string chave, CancellationToken cancellationToken = default)
    {
        ValidarOperacao(cancellationToken);
        chave = chave?.Trim();
        NFeValidacao.Chave(chave);
        using var certificate = await ObterCertificadoAsync(cancellationToken).ConfigureAwait(false);
        using var service = new NFeConsultaServiceClient(configuracao, certificate);
        return await service.ConsultarAsync(chave, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NFeReciboResposta> ConsultaReciboAsync(NFeReciboSolicitacao solicitacao,
        CancellationToken cancellationToken = default)
    {
        ValidarOperacao(cancellationToken);
        if (solicitacao == null) throw new ArgumentNullException(nameof(solicitacao));
        if (configuracao.Geral.Modelo == ModeloNFe.NFCe)
            throw new NotSupportedException("NFC-e utiliza autorização síncrona, sem consulta de recibo.");
        var recibo = solicitacao.Recibo;
        Numero(recibo, 15, 15, nameof(solicitacao.Recibo));
        var notas = CopiarDocumentos(solicitacao.Documentos);
        foreach (var nota in notas) ValidarDocumento(configuracao, nota);
        using var certificate = await ObterCertificadoAsync(cancellationToken).ConfigureAwait(false);
        using var service = new NFeRetAutorizacaoServiceClient(configuracao, certificate);
        var result = await service.ConsultarAsync(recibo, cancellationToken).ConfigureAwait(false);
        // Sem XMLs explícitos, devolve os protocolos para associação pela aplicação.
        result.Documentos = notas.Length == 0 ? [] : NFeAutorizador.Protocolar(result.Resultado.Protocolos, notas);
        await NFeAutorizador.SalvarDocumentosAsync(configuracao, result.Documentos, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<NFeEventoResposta> EnviarEventoAsync(NFeEventoSolicitacao solicitacao,
        CancellationToken cancellationToken = default)
    {
        ValidarOperacao(cancellationToken);
        if (solicitacao == null) throw new ArgumentNullException(nameof(solicitacao));
        var lote = solicitacao.Lote;
        Numero(lote, 1, 15, nameof(solicitacao.Lote));
        var lista = solicitacao.Eventos?.Select(CopiarEvento).ToArray() ??
                    throw new ArgumentNullException(nameof(solicitacao.Eventos));
        if (lista.Length is < 1 or > 20) throw new ArgumentException("Lote deve conter de 1 a 20 eventos.");
        var nacional = lista.All(item => item.Orgao == 91);
        if (lista.Any(item => item.Orgao == 91) && !nacional)
            throw new ArgumentException("Separe eventos nacionais e estaduais em lotes distintos.");
        foreach (var item in lista)
        {
            NFeValidacao.Chave(item.Chave);
            if (item.Chave.Substring(20, 2) != ((int)configuracao.Geral.Modelo).ToString(CultureInfo.InvariantCulture))
                throw new ArgumentException("Modelo do evento difere da configuração.");
        }

        if (lista.Select(item => item.Chave + ":" + item.Codigo + ":" + item.Sequencia).Distinct().Count() !=
            lista.Length) throw new ArgumentException("Evento duplicado no lote.");
        using var certificate = await ObterCertificadoAsync(cancellationToken).ConfigureAwait(false);
        var configuracaoEvento = configuracao.Copiar();
        configuracaoEvento.Certificado = certificate;
        var assinados = lista.Select(item => item.Assinar(configuracaoEvento)).ToArray();
        using var service = new NFeRecepcaoEventoServiceClient(configuracao, certificate, nacional);
        var result = await service.ReceberAsync(lote, assinados, cancellationToken).ConfigureAwait(false);
        result.Eventos = assinados.Select(item =>
        {
            var retorno = result.Resultado.Eventos.SingleOrDefault(value =>
                value.InfEvento?.Chave == item.Chave && value.InfEvento.Codigo == item.InfEvento.Codigo &&
                value.InfEvento.Sequencia == item.InfEvento.Sequencia);
            return retorno == null ? null : new NFeProcEvento { Evento = item, RetEvento = retorno };
        }).Where(item => item != null).ToArray();
        foreach (var item in result.Eventos)
        {
            var info = item.Evento.InfEvento;
            await SalvarAsync(configuracao, item.GetXml(), "Evento", info.Id + "-procEventoNFe.xml",
                item.Evento.DocumentoAutor, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<NFeCadastroResposta> ConsultaCadastroAsync(string documento, string tipo = "CNPJ",
        CancellationToken cancellationToken = default)
    {
        ValidarOperacao(cancellationToken);
        if (tipo is not ("CNPJ" or "CPF" or "IE"))
            throw new ArgumentException("Tipo de documento deve ser CNPJ, CPF ou IE.");
        if (string.IsNullOrWhiteSpace(documento))
            throw new ArgumentException("Documento não informado.", nameof(documento));
        if (tipo is "CNPJ" or "CPF")
        {
            NFeValidacao.Documento(documento);
            if (documento.Length != (tipo == "CPF" ? 11 : 14))
                throw new ArgumentException("Tipo de documento incompatível com o conteúdo.");
        }

        using var certificate = await ObterCertificadoAsync(cancellationToken).ConfigureAwait(false);
        using var service = new NFeConsultaCadastroServiceClient(configuracao, certificate);
        return await service.ConsultarAsync(documento, tipo, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NFeInutilizacaoResposta> InutilizarAsync(NFeInutilizacaoSolicitacao solicitacao,
        CancellationToken cancellationToken = default)
    {
        ValidarOperacao(cancellationToken);
        if (solicitacao == null) throw new ArgumentNullException(nameof(solicitacao));
        var pedido = new NFeInutilizacaoSolicitacao
        {
            Cnpj = solicitacao.Cnpj, Ano = solicitacao.Ano, Serie = solicitacao.Serie,
            NumeroInicial = solicitacao.NumeroInicial, NumeroFinal = solicitacao.NumeroFinal,
            Justificativa = solicitacao.Justificativa
        };
        NFeValidacao.Documento(pedido.Cnpj);
        if (pedido.Cnpj.Length != 14) throw new ArgumentException("Inutilização exige CNPJ.");
        if (pedido.Ano < 0 || pedido.Ano > 99 || pedido.Serie < 0 || pedido.Serie > 999 || pedido.NumeroInicial < 1 ||
            pedido.NumeroFinal < pedido.NumeroInicial ||
            pedido.NumeroFinal > 999999999)
            throw new ArgumentException("Faixa, ano (dois dígitos) ou série inválidos.");
        using var certificate = await ObterCertificadoAsync(cancellationToken).ConfigureAwait(false);
        using var service = new NFeInutilizacaoServiceClient(configuracao, certificate);
        var result = await service.InutilizarAsync(pedido, cancellationToken).ConfigureAwait(false);
        if (result.Documento != null)
            await SalvarAsync(configuracao, result.Documento.GetXml(), "Inutilizacao",
                    result.Documento.Inutilizacao.InfInut.Id + "-procInutNFe.xml", pedido.Cnpj, cancellationToken)
                .ConfigureAwait(false);

        return result;
    }

    public async Task<NFeDistribuicaoResposta> ConsultarDistribuicaoAsync(NFeDistribuicaoSolicitacao solicitacao,
        CancellationToken cancellationToken = default)
    {
        ValidarOperacao(cancellationToken);
        if (solicitacao == null) throw new ArgumentNullException(nameof(solicitacao));
        var pedido = new NFeDistribuicaoSolicitacao
        {
            CpfCnpj = solicitacao.CpfCnpj, UltimoNsu = solicitacao.UltimoNsu, Nsu = solicitacao.Nsu,
            Chave = solicitacao.Chave
        };
        if (configuracao.Geral.Modelo != ModeloNFe.NFe)
            throw new NotSupportedException("Distribuição DF-e disponível para modelo 55.");
        if (new[] { pedido.UltimoNsu, pedido.Nsu, pedido.Chave }.Count(value => value != null) != 1)
            throw new ArgumentException("Informe exatamente um filtro: último NSU, NSU ou chave.");
        if (pedido.Chave != null) NFeValidacao.Chave(pedido.Chave);
        else Numero(pedido.Nsu ?? pedido.UltimoNsu, 15, 15, nameof(solicitacao));
        NFeValidacao.Documento(pedido.CpfCnpj);
        using var certificate = await ObterCertificadoAsync(cancellationToken).ConfigureAwait(false);
        using var service = new NFeDistribuicaoServiceClient(configuracao, certificate);
        return await service.ConsultarAsync(pedido, cancellationToken).ConfigureAwait(false);
    }

    private void ValidarOperacao(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (configuracao.Geral.VersaoDFe != NFeVersao.v400)
            throw new NotSupportedException(
                "Leiaute 3.10 permite somente leitura, geração e impressão. Comunicação exige 4.00.");
        if (configuracao.Geral.Modelo is not (ModeloNFe.NFe or ModeloNFe.NFCe))
            throw new ArgumentException("Modelo deve ser 55 ou 65.");
        if (configuracao.WebServices.TimeoutOperacao <= TimeSpan.Zero)
            throw new ArgumentException("Timeout deve ser positivo.");
    }

    private async Task<X509Certificate2> ObterCertificadoAsync(CancellationToken token)
    {
        var options = new NFeConfigCertificados
        {
            Certificado = configuracao.Certificados.Certificado, Senha = configuracao.Certificados.Senha,
            CertificadoBytes = configuracao.Certificados.CertificadoBytes == null
                ? null
                : (byte[])configuracao.Certificados.CertificadoBytes.Clone()
        };
        options.MargemValidadeCertificado = configuracao.Certificados.MargemValidadeCertificado;
        var certificate = await certificados.ObterAsync(options, token).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested();
            configuracao.Certificados.ValidarCertificado(certificate);
            if (certificate == null || !certificate.HasPrivateKey)
                throw new InvalidOperationException("Certificado com chave privada não informado.");
            return certificate;
        }
        catch
        {
            certificate?.Dispose();
            throw;
        }
    }

    private static void ValidarDocumento(NFeConfig configuracao, Common.Model.NFe nota)
    {
        if (nota.InfNFe?.Versao != NFeVersao.v400 || nota.Modelo != configuracao.Geral.Modelo)
            throw new OpenDFeValidationException("Versão/modelo da nota diferem da configuração.");
        var ide = nota.InfNFe.Ide;
        if (ide?.TpAmb != configuracao.WebServices.Ambiente ||
            ide.CUF != configuracao.WebServices.UF.ToCodeUf() ||
            ide.TpEmis.GetDFeValue() != configuracao.Geral.FormaEmissao.GetDFeValue())
            throw new OpenDFeValidationException("UF, ambiente ou emissão da nota diferem da configuração.");
    }

    private static Common.Model.NFe[] CopiarDocumentos(IEnumerable<Common.Model.NFe> documentos)
    {
        var docs = documentos?.ToArray() ?? throw new ArgumentNullException(nameof(documentos));
        if (docs.Any(item => item == null)) throw new ArgumentException("Documento nulo no lote.");
        var copies = docs.Select(NFeDocumentCopy.Copiar).ToArray();
        var keys = copies.Select(item => item.Chave).Where(key => !string.IsNullOrWhiteSpace(key)).ToArray();
        if (keys.Distinct().Count() != keys.Length) throw new ArgumentException("Chave duplicada no lote.");
        return copies;
    }

    private static NFeEvento CopiarEvento(NFeEvento item) => item == null
        ? throw new ArgumentException("Evento nulo.")
        : NFeDocumentCopy.Copiar(item);

    private static Task SalvarAsync(NFeConfig configuracao, string xml, string categoria, string name,
        string contribuinte, CancellationToken token) =>
        !configuracao.Arquivos.Salvar
            ? Task.CompletedTask
            : configuracao.DocumentStore.SaveAsync(
                new DFeDocument(
                    Path.GetFullPath(Path.Combine(configuracao.Arquivos.GetDocumentPath(categoria, contribuinte),
                        name)), xml, DFeArquivoExiste.Sobrescrever), token);

    private static void Numero(string number, int min, int max, string param)
    {
        if (number == null || number.Length < min || number.Length > max ||
            number.Any(value => value is < '0' or > '9'))
            throw new ArgumentException("Informe de " + min + " a " + max + " dígitos.", param);
    }
}