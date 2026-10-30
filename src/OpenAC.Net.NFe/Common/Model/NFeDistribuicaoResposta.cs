using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace OpenAC.Net.NFe.Common.Model;

public sealed class NFeDistribuicaoResposta : NFeResposta<NFeDistribuicaoResult>
{
    public NFeDistribuicaoResposta(string xmlEnvio, string xmlRetorno, string envelopeEnvio = "", string envelopeRetorno = "")
        : base(xmlEnvio, xmlRetorno, envelopeEnvio, envelopeRetorno) { }

    public string UltNSU => Resultado.UltNSU ?? "";
    public string MaxNSU => Resultado.MaxNSU ?? "";

    /// <summary>Descompacta docZip com limite de 16 MiB por documento.</summary>
    public IReadOnlyList<NFeDocumentoDistribuido> DocumentosDistribuidos()
    {
        var documentos = new List<NFeDocumentoDistribuido>();
        if (Resultado.Lote == null) return documentos;
        foreach (var documento in Resultado.Lote.Documentos)
        {
            using var entrada = new MemoryStream(Convert.FromBase64String(documento.Conteudo));
            using var gzip = new GZipStream(entrada, CompressionMode.Decompress);
            using var saida = new MemoryStream();
            var buffer = new byte[8192];
            int lidos;
            while ((lidos = gzip.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (saida.Length + lidos > 16 * 1024 * 1024)
                    throw new InvalidDataException("docZip excede o limite de 16 MiB.");
                saida.Write(buffer, 0, lidos);
            }
            documentos.Add(new NFeDocumentoDistribuido(documento.NSU, documento.Schema,
                Encoding.UTF8.GetString(saida.ToArray())));
        }
        return documentos;
    }
}
