using System;
using System.IO;
using System.Text.RegularExpressions;
using OpenAC.Net.DFe.Core;

namespace OpenAC.Net.NFe.Common;

internal static class NFeValidacao
{
    internal static void Documento(string documento)
    {
        if (documento == null || !Regex.IsMatch(documento, "^([0-9]{11}|[0-9A-Z]{12}[0-9]{2})$"))
            throw new ArgumentException("CPF/CNPJ inválido. Informe somente números ou letras maiúsculas permitidas.", nameof(documento));
    }

    internal static void Chave(string chave)
    {
        if (!NFeChave.Validar(chave))
            throw new ArgumentException("Chave de acesso inválida; informe 44 posições com DV válido.", nameof(chave));
    }

    internal static void Schema(string xml, string arquivo)
    {
        if (!File.Exists(arquivo)) throw new FileNotFoundException("Schema da NF-e não encontrado.", arquivo);
        if (!XmlSchemaValidation.ValidarXml(xml, arquivo, out var erros, out _))
            throw new OpenDFeValidationException(string.Join(Environment.NewLine, erros));
    }
}
