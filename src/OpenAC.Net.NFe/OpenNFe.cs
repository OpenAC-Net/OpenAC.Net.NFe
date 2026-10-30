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

/// <summary>Cliente assíncrono sem coleção compartilhada. Configuração e dados são isolados por operação.</summary>
public sealed class OpenNFe(NFeConfig configuracao, INFeCertificadoProvider certificados = null)
    : IOpenNFeClient
{
    private readonly NFeConfig configuracao = (configuracao ?? throw new ArgumentNullException(nameof(configuracao))).Copiar();
    private readonly INFeCertificadoProvider certificados = certificados ?? new NFeCertificadoProvider(configuracao.Certificado);

    public async Task<NFeAutorizacaoResposta> EnviarAsync(NFeEnvioSolicitacao solicitacao,
        CancellationToken cancellationToken = default)
    {
        ValidarOperacao(cancellationToken);
        if (solicitacao == null) throw new ArgumentNullException(nameof(solicitacao));
        var lote = solicitacao.Lote;
        var sincrono = solicitacao.Sincrono;
        Numero(lote, 1, 15, nameof(solicitacao.Lote));
        var notas = CopiarDocumentos(solicitacao.Documentos);
        if (notas.Length < 1 || notas.Length > 50 || sincrono && notas.Length != 1)
            throw new ArgumentException("Lote deve conter 1 a 50 notas; envio síncrono exige uma nota.");
        if (configuracao.Geral.Modelo == ModeloNFe.NFCe && (!sincrono || notas.Length != 1))
            throw new ArgumentException("NFC-e deve ser enviada individualmente e de forma síncrona.");
        foreach (var nota in notas) ValidarDocumento(nota);
        using var certificate = await ObterCertificadoAsync(cancellationToken).ConfigureAwait(false);
        var config = CriarConfigOperacao(certificate);
        foreach (var nota in notas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!nota.Assinado) nota.Assinar(certificate);
            if (nota.Modelo == ModeloNFe.NFCe && string.IsNullOrEmpty(nota.QrCode)) nota.GerarQrCode(config);
            nota.ValidarIdentificacao();
            if (!nota.ValidarAssinatura()) throw new OpenDFeValidationException("Assinatura inválida.");
            nota.Validar(config.Arquivos.PathSchemas);
        }

        if (notas.Select(nota => nota.Chave).Distinct().Count() != notas.Length)
            throw new ArgumentException("Chave duplicada no lote.");
        using var service = new NFeAutorizacaoServiceClient(config, certificate);
        var result = await service.AutorizarAsync(lote, sincrono, notas, cancellationToken).ConfigureAwait(false);
        result.DocumentosEnviados = notas;
        result.Documentos = Protocolar(result.Resultado.Protocolos, notas);
        await SalvarDocumentosAsync(result.Documentos, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<NFeStatusServicoResposta> ConsultarSituacaoServicoAsync(CancellationToken cancellationToken = default)
    {
        ValidarOperacao(cancellationToken);
        using var certificate = await ObterCertificadoAsync(cancellationToken).ConfigureAwait(false);
        using var service = new NFeStatusServicoServiceClient(configuracao, certificate);
        return await service.ConsultarAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<NFeConsultaResposta> ConsultarAsync(string chave, CancellationToken cancellationToken = default)
    {
        ValidarOperacao(cancellationToken);
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
        foreach (var nota in notas) ValidarDocumento(nota);
        using var certificate = await ObterCertificadoAsync(cancellationToken).ConfigureAwait(false);
        using var service = new NFeRetAutorizacaoServiceClient(configuracao, certificate);
        var result = await service.ConsultarAsync(recibo, cancellationToken).ConfigureAwait(false);
        // Sem XMLs explícitos, devolve os protocolos para associação pela aplicação.
        result.Documentos = notas.Length == 0 ? [] : Protocolar(result.Resultado.Protocolos, notas);
        await SalvarDocumentosAsync(result.Documentos, cancellationToken).ConfigureAwait(false);
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
        if (lista.Length < 1 || lista.Length > 20) throw new ArgumentException("Lote deve conter de 1 a 20 eventos.");
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
        var config = CriarConfigOperacao(certificate);
        var assinados = lista.Select(item => item.Assinar(config)).ToArray();
        using var service = new NFeRecepcaoEventoServiceClient(config, certificate, nacional);
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
            await SalvarAsync(item.GetXml(), "Evento", info.Id + "-procEventoNFe.xml",
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
            await SalvarAsync(result.Documento.GetXml(), "Inutilizacao",
                result.Documento.Inutilizacao.InfInut.Id + "-procInutNFe.xml", pedido.Cnpj, cancellationToken)
                .ConfigureAwait(false);

        return result;
    }

    public async Task<NFeDistribuicaoResposta> DistribuicaoDFeAsync(NFeDistribuicaoSolicitacao solicitacao,
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
        var certificate = await certificados.ObterAsync(options, token).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested();
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

    private NFeConfig CriarConfigOperacao(X509Certificate2 certificate)
    {
        var config = configuracao.Copiar();
        config.Certificado = certificate;
        return config;
    }

    private void ValidarDocumento(Common.Model.NFe nota)
    {
        if (nota.InfNFe?.Versao != NFeVersao.v400 || nota.Modelo != configuracao.Geral.Modelo)
            throw new OpenDFeValidationException("Versão/modelo da nota diferem da configuração.");
        var ide = nota.InfNFe.Ide;
        if (ide?.TpAmb != ((int)configuracao.WebServices.Ambiente).ToString(CultureInfo.InvariantCulture) ||
            ide.CUF != ((int)configuracao.WebServices.UF.ToCodeUf()).ToString(CultureInfo.InvariantCulture) ||
            ide.TpEmis != configuracao.Geral.FormaEmissao.GetDFeValue())
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

    private static IReadOnlyList<NFeProc> Protocolar(IEnumerable<NFeProtNFe> protocolos, IEnumerable<Common.Model.NFe> notas)
    {
        var docs = new List<NFeProc>();
        foreach (var protocolo in protocolos)
        {
            if (protocolo.InfProt?.CStat is not (100 or 150)) continue;
            var nota = notas.SingleOrDefault(item => item.Chave == protocolo.InfProt.ChNFe);
            if (nota == null) throw new OpenDFeValidationException("Protocolo retornado para chave fora do lote.");
            if (docs.Any(item => item.Chave == nota.Chave))
                throw new OpenDFeValidationException("Protocolo duplicado para a mesma NF-e.");
            nota.ValidarIdentificacao();
            if (!nota.ValidarAssinatura()) throw new OpenDFeValidationException("Assinatura inválida.");
            docs.Add(nota.ComProtocolo(protocolo));
        }
        return docs;
    }

    private async Task SalvarDocumentosAsync(IEnumerable<NFeProc> notas, CancellationToken token)
    {
        foreach (var nota in notas)
        {
            var emit = nota.NFe.InfNFe.Emit;
            var contribuinte = emit.CNPJ ?? emit.CPF;
            await SalvarAsync(nota.GetXml(), nota.Modelo.ToString(), nota.Chave + "-procNFe.xml", contribuinte, token)
                .ConfigureAwait(false);
        }
    }

    private Task SalvarAsync(string xml, string categoria, string name, string contribuinte, CancellationToken token) =>
        !configuracao.Arquivos.Salvar
            ? Task.CompletedTask
            : configuracao.DocumentStore.SaveAsync(
                new DFeDocument(
                    Path.GetFullPath(Path.Combine(configuracao.Arquivos.GetDocumentPath(categoria, contribuinte),
                        name)), xml, DFeArquivoExiste.Sobrescrever), token);

    private static void Numero(string number, int min, int max, string param)
    {
        if (number == null || number.Length < min || number.Length > max ||
            number.Any(value => value < '0' || value > '9'))
            throw new ArgumentException("Informe de " + min + " a " + max + " dígitos.", param);
    }
}