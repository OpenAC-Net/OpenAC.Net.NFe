using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.DFe.Core;
using OpenAC.Net.DFe.Core.Storage;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;
using NotaFiscal = OpenAC.Net.NFe.Common.Model.NFe;

namespace OpenAC.Net.NFe.Services;

internal static class NFeAutorizador
{
    internal static async Task<NFeAutorizacaoResposta> AutorizarAsync(NFeConfig configuracao, string lote,
        bool sincrono, NotaFiscal[] notas, X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        foreach (var nota in notas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!nota.Assinado) nota.Assinar(certificate);
            if (nota.Modelo == ModeloNFe.NFCe && string.IsNullOrEmpty(nota.QrCode)) nota.GerarQrCode(configuracao);
            nota.ValidarIdentificacao();
            if (!nota.ValidarAssinatura()) throw new OpenDFeValidationException("Assinatura inválida.");
            nota.Validar(configuracao.Arquivos.PathSchemas);
        }

        if (notas.Select(nota => nota.Chave).Distinct().Count() != notas.Length)
            throw new ArgumentException("Chave duplicada no lote.");
        using var service = new Recepcao.NFeAutorizacaoServiceClient(configuracao, certificate);
        var result = await service.AutorizarAsync(lote, sincrono, notas, cancellationToken).ConfigureAwait(false);
        result.DocumentosEnviados = notas;
        result.Documentos = Protocolar(result.Resultado.Protocolos, notas);
        await SalvarDocumentosAsync(configuracao, result.Documentos, cancellationToken).ConfigureAwait(false);
        return result;
    }

    internal static IReadOnlyList<NFeProc> Protocolar(IEnumerable<NFeProtNFe> protocolos,
        IEnumerable<NotaFiscal> notas)
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

    internal static async Task SalvarDocumentosAsync(NFeConfig configuracao, IEnumerable<NFeProc> notas,
        CancellationToken token)
    {
        foreach (var nota in notas)
        {
            var emit = nota.NFe.InfNFe.Emit;
            var contribuinte = emit.CNPJ ?? emit.CPF;
            await SalvarAsync(configuracao, nota.GetXml(), nota.Modelo.ToString(), nota.Chave + "-procNFe.xml",
                    contribuinte, token)
                .ConfigureAwait(false);
        }
    }

    private static Task SalvarAsync(NFeConfig configuracao, string xml, string categoria, string name,
        string contribuinte, CancellationToken token) =>
        !configuracao.Arquivos.Salvar
            ? Task.CompletedTask
            : configuracao.DocumentStore.SaveAsync(
                new DFeDocument(
                    Path.GetFullPath(Path.Combine(configuracao.Arquivos.GetDocumentPath(categoria, contribuinte),
                        name)), xml, DFeArquivoExiste.Sobrescrever), token);
}
