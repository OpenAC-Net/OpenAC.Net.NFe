using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using OpenAC.Net.DFe.Core.Attributes;
using OpenAC.Net.NFe.Common.Model;
using OpenAC.Net.NFe.Common.Types;
using NotaFiscal = OpenAC.Net.NFe.Common.Model.NFe;

namespace OpenAC.Net.NFe.DANFe;

/// <summary>Apresentação dos dados fiscais usados pelos DANFEs PDF e ESC/POS.</summary>
internal static class NFeImpressao
{
    internal static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    internal static NFeProc ParaImpressao(NotaFiscal nota)
    {
        if (nota?.InfNFe == null) throw new ArgumentException("Informações da NF-e não informadas.", nameof(nota));
        return new NFeProc { NFe = nota, Versao = nota.InfNFe.Versao };
    }

    internal static string Texto(object valor)
    {
        if (valor is Enum)
        {
            var member = valor.GetType().GetField(valor.ToString());
            var attribute = member?.GetCustomAttribute<DFeEnumAttribute>();
            if (attribute != null) return attribute.Value;
        }
        return Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "";
    }
    internal static string Dinheiro(decimal? valor) => (valor ?? 0).ToString("N2", Cultura);
    internal static string PrecoUnitario(decimal valor) => valor.ToString("0.00########", Cultura);

    internal static string Numero(decimal? valor, int casas = 4) =>
        valor?.ToString("0." + new string('#', casas), Cultura) ?? "";

    internal static string Data(object valor)
    {
        var texto = Texto(valor);
        return DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data.ToString("dd/MM/yyyy HH:mm:ss", Cultura)
            : texto;
    }

    internal static string Documento(string valor)
    {
        if (string.IsNullOrEmpty(valor)) return "";
        if (valor.Length == 14)
            return valor.Substring(0, 2) + "." + valor.Substring(2, 3) + "." + valor.Substring(5, 3) + "/" +
                   valor.Substring(8, 4) + "-" + valor.Substring(12);
        if (valor.Length == 11)
            return valor.Substring(0, 3) + "." + valor.Substring(3, 3) + "." + valor.Substring(6, 3) + "-" +
                   valor.Substring(9);
        return valor;
    }

    internal static string Chave(string chave) => string.Join(" ",
        Enumerable.Range(0, (chave?.Length ?? 0) / 4).Select(i => chave.Substring(i * 4, 4)));

    internal static string Endereco(object endereco) => string.Join(" ", new[]
    {
        Valor(endereco, "XLgr"), Valor(endereco, "Nro"), Valor(endereco, "XCpl"), Valor(endereco, "XBairro"),
        Valor(endereco, "XMun"), Valor(endereco, "UF"), Valor(endereco, "CEP")
    }.Where(item => !string.IsNullOrWhiteSpace(item)));

    internal static IEnumerable<string> Avisos(NFeProc nota)
    {
        var ide = nota.NFe.InfNFe.Ide;
        if (nota.Cancelado || nota.ProtNFe?.InfProt?.CStat is 101 or 151 or 155)
            yield return "DOCUMENTO CANCELADO - SEM VALOR FISCAL";
        if (nota.ProtNFe?.InfProt?.CStat is 110 or 301 or 302)
            yield return "USO DENEGADO - SEM VALOR FISCAL";
        if (Texto(ide.TpAmb) == "2") yield return "EMITIDA EM AMBIENTE DE HOMOLOGAÇÃO - SEM VALOR FISCAL";
        if (Texto(ide.TpEmis) == "9")
        {
            yield return "EMITIDA EM CONTINGÊNCIA";
            if (!nota.Autorizado) yield return "Pendente de autorização";
            yield return "Data da contingência: " + Data(ide.DhCont);
            if (!string.IsNullOrEmpty(ide.XJust)) yield return ide.XJust;
        }
        else if (!nota.Autorizado) yield return "SEM AUTORIZAÇÃO DE USO";
    }

    internal static string Frete(object codigo) => Texto(codigo) switch
    {
        "0" => "0 - Emitente (CIF)", "1" => "1 - Destinatário (FOB)", "2" => "2 - Terceiros",
        "3" => "3 - Transporte próprio do emitente", "4" => "4 - Transporte próprio do destinatário",
        "9" => "9 - Sem transporte", _ => Texto(codigo)
    };

    internal static string Pagamento(string codigo) => codigo switch
    {
        "01" => "Dinheiro", "02" => "Cheque", "03" => "Cartão de crédito", "04" => "Cartão de débito",
        "05" => "Crédito loja",
        "10" => "Vale alimentação", "11" => "Vale refeição", "12" => "Vale presente", "13" => "Vale combustível",
        "15" => "Boleto",
        "16" => "Depósito", "17" => "PIX", "18" => "Transferência", "19" => "Fidelidade", "20" => "PIX estático",
        "21" => "Crédito em loja", "22" => "Pagamento eletrônico não informado", "90" => "Sem pagamento",
        "99" => "Outros", _ => "Pagamento " + codigo
    };

    internal sealed class PagamentoImpressao(string descricao, decimal valor)
    {
        internal string Descricao { get; } = descricao;
        internal decimal Valor { get; } = valor;
    }

    internal static IEnumerable<PagamentoImpressao> Pagamentos(NFeInfNFe info)
    {
        foreach (var pagamento in info.Pag)
        {
            if (info.Versao == NFeVersao.v310)
                yield return new PagamentoImpressao(Pagamento(Texto(pagamento.TPag)), pagamento.VPag);
            else
                foreach (var detalhe in pagamento.DetPag)
                    yield return new PagamentoImpressao(
                        Texto(detalhe.TPag) == "99" && !string.IsNullOrWhiteSpace(detalhe.XPag)
                            ? detalhe.XPag
                            : Pagamento(Texto(detalhe.TPag)), detalhe.VPag);
        }
    }

    internal static object Alternativa(object grupo) => grupo switch
    {
        NFeDetImpostoICMS icms => icms.Icms,
        NFeDetImpostoIPI ipi => ipi.TipoIPI,
        NFeDetImpostoPIS pis => pis.TipoPIS,
        NFeDetImpostoCOFINS cofins => cofins.TipoCOFINS,
        _ => grupo?.GetType().GetProperties()
        .Where(p => p.GetCustomAttribute<DFeElementAttribute>() != null)
        .Select(p => p.GetValue(grupo)).FirstOrDefault(valor => valor != null)
    };

    internal static string Valor(object grupo, string propriedade) =>
        Texto(grupo?.GetType().GetProperty(propriedade)?.GetValue(grupo));

    internal static decimal? Decimal(object grupo, string propriedade)
    {
        var valor = grupo?.GetType().GetProperty(propriedade)?.GetValue(grupo);
        return valor == null ? null : Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
    }

    private static readonly IReadOnlyDictionary<string, string> Rotulos = new Dictionary<string, string>
    {
        ["Descricao"] = "Descrição", ["Correcao"] = "Correção", ["CondicaoUso"] = "Condições de uso",
        ["OrgaoAutor"] = "Órgão do autor", ["TipoAutor"] = "Tipo do autor",
        ["VersaoAplicativo"] = "Versão do aplicativo",
        ["ChaveReferenciada"] = "Chave referenciada", ["VBCIBSCBS"] = "Base de cálculo IBS/CBS",
        ["VBC"] = "Base de cálculo",
        ["VIBS"] = "Valor do IBS", ["VIBSUF"] = "IBS estadual", ["VIBSMun"] = "IBS municipal",
        ["VCBS"] = "Valor da CBS",
        ["VIBSMono"] = "IBS monofásico", ["VCBSMono"] = "CBS monofásica", ["VISS"] = "Valor do ISS",
        ["VServ"] = "Valor dos serviços",
        ["VPIS"] = "Valor do PIS", ["VCOFINS"] = "Valor da COFINS", ["DCompet"] = "Competência",
        ["VISSRet"] = "ISS retido"
    };

    private static string Rotulo(string propriedade) =>
        Rotulos.TryGetValue(propriedade, out var rotulo) ? rotulo : propriedade;

    internal static IEnumerable<string> Campos(object grupo) => Campos(grupo, 0);

    private static IEnumerable<string> Campos(object grupo, int nivel)
    {
        if (grupo == null || nivel > 12) yield break;
        if (grupo is IEnumerable lista && grupo is not string)
        {
            foreach (var item in lista)
            foreach (var campo in Campos(item, nivel + 1))
                yield return campo;
            yield break;
        }

        foreach (var propriedade in grupo.GetType().GetProperties().OrderBy(p => p.MetadataToken))
        {
            if (propriedade.GetIndexParameters().Length > 0 ||
                propriedade.GetCustomAttribute<DFeIgnoreAttribute>() != null) continue;
            if (propriedade.GetCustomAttribute<DFeElementAttribute>() == null &&
                propriedade.GetCustomAttribute<DFeCollectionAttribute>() == null) continue;
            var condicao = grupo.GetType().GetMethod("ShouldSerialize" + propriedade.Name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (condicao?.ReturnType == typeof(bool) && condicao.GetParameters().Length == 0 &&
                condicao.Invoke(grupo, null) is false) continue;
            var valor = propriedade.GetValue(grupo);
            if (valor == null) continue;
            if (valor is string texto)
            {
                if (texto.Length > 0) yield return Rotulo(propriedade.Name) + ": " + texto;
            }
            else if (valor is decimal numero)
                yield return Rotulo(propriedade.Name) + ": " + numero.ToString("0.####", Cultura);
            else if (valor.GetType().IsValueType) yield return Rotulo(propriedade.Name) + ": " + Texto(valor);
            else
                foreach (var campo in Campos(valor, nivel + 1))
                    yield return campo;
        }
    }
}