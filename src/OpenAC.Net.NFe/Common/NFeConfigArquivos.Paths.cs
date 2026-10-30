using System;
using System.Collections.Generic;
using System.IO;
using OpenAC.Net.DFe.Core.Common;

namespace OpenAC.Net.NFe.Common;

public sealed partial class NFeConfigArquivos
{
    /// <summary>Resolve o caminho sem criar pastas; a criação pertence ao IDFeDocumentStore.</summary>
    public override string GetDocumentPath(string categoria, string documentoContribuinte = null, DateTime? data = null)
    {
        if (string.IsNullOrWhiteSpace(categoria) || categoria is "." or ".." || categoria.IndexOfAny(['/', '\\', ':']) >= 0 || categoria.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Categoria inválida.", nameof(categoria));
        if (!string.IsNullOrEmpty(documentoContribuinte)) NFeValidacao.Documento(documentoContribuinte);
        var order = OrdenacaoPath.ToArray();
        if (order.Length == 0)
        {
            var defaults = new List<TagOrdenacaoPath>();
            if (SepararPorCNPJ) defaults.Add(TagOrdenacaoPath.CNPJ);
            if (SepararPorModelo) defaults.Add(TagOrdenacaoPath.Modelo);
            if (SepararPorAno || SepararPorMes || SepararPorDia) defaults.Add(TagOrdenacaoPath.Data);
            if (AdicionarLiteral) defaults.Add(TagOrdenacaoPath.Literal);
            order = [.. defaults];
        }
        var path = PathSalvar;
        var date = data ?? DateTime.Now;
        foreach (var part in order)
        {
            switch (part)
            {
                case TagOrdenacaoPath.CNPJ:
                    if (!string.IsNullOrEmpty(documentoContribuinte)) path = Path.Combine(path, documentoContribuinte);
                    break;
                case TagOrdenacaoPath.Modelo: path = Path.Combine(path, geral.Modelo.ToString()); break;
                case TagOrdenacaoPath.Data:
                    if (SepararPorAno) path = Path.Combine(path, date.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture));
                    if (SepararPorMes) path = Path.Combine(path, date.ToString("MM", System.Globalization.CultureInfo.InvariantCulture));
                    if (SepararPorDia) path = Path.Combine(path, date.ToString("dd", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case TagOrdenacaoPath.Literal: path = Path.Combine(path, categoria); break;
                case TagOrdenacaoPath.Nenhum: break;
                default: throw new ArgumentOutOfRangeException(nameof(OrdenacaoPath));
            }
        }
        return path;
    }
}
