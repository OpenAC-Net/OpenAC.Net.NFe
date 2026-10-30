using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenAC.Net.NFe.Common;

/// <summary>Chave com CNPJ numérico ou alfanumérico (NT Conjunta 2025.001, ASCII menos 48).</summary>
public static class NFeChave
{
    public static int Digito(string base43)
    {
        if (base43 == null || !Regex.IsMatch(base43, "^[0-9]{6}[0-9A-Z]{12}[0-9]{25}$")) throw new ArgumentException("Base da chave deve ter 43 posições.");
        int sum = 0, weight = 2;
        for (var i = 42; i >= 0; i--) { sum += (base43[i] - 48) * weight; if (++weight > 9) weight = 2; }
        var dv = 11 - sum % 11; return dv >= 10 ? 0 : dv;
    }
    public static bool Validar(string chave) => chave != null && Regex.IsMatch(chave, "^[0-9]{6}[0-9A-Z]{12}[0-9]{26}$") && Digito(chave.Substring(0, 43)) == chave[43] - '0';
    internal static string Gerar(int uf, DateTimeOffset emissao, string documento, int modelo, int serie, long numero, string tipoEmissao, int codigo)
    {
        if (serie < 0 || serie > 999 || numero < 1 || numero > 999999999 || codigo < 0 || codigo > 99999999 || tipoEmissao.Length != 1)
            throw new ArgumentException("Campos da chave fora dos limites.");
        var value = uf.ToString("D2", CultureInfo.InvariantCulture) + emissao.ToString("yyMM", CultureInfo.InvariantCulture) + documento + modelo.ToString("D2", CultureInfo.InvariantCulture) + serie.ToString("D3", CultureInfo.InvariantCulture) + numero.ToString("D9", CultureInfo.InvariantCulture) + tipoEmissao + codigo.ToString("D8", CultureInfo.InvariantCulture);
        return value + Digito(value).ToString(CultureInfo.InvariantCulture);
    }
}
