using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using OpenAC.Net.DFe.Core.Extensions;
using OpenAC.Net.NFe.Common.Types;
using OpenAC.Net.NFe.Services;

namespace OpenAC.Net.NFe.Common;

/// <summary>QR Code NFC-e 2 (CSC) e 3 (sem CSC), emissão normal e contingência offline.</summary>
public static class NFCeQrCode
{
    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");

    public static string Gerar(Model.NFe nota, NFeConfig config)
    {
        if (nota == null || config == null)
            throw new ArgumentNullException(nota == null ? nameof(nota) : nameof(config));
        if (nota.Modelo != ModeloNFe.NFCe) throw new ArgumentException("QR Code exige modelo 65.");
        NFeValidacao.Chave(nota.Chave);
        var dados = nota.InfNFe;
        var ide = dados.Ide;
        var ambiente = ide.TpAmb;
        if (ambiente != ((int)config.WebServices.Ambiente).ToString(CultureInfo.InvariantCulture) ||
            ide.CUF != ((int)config.WebServices.UF.ToCodeUf()).ToString(CultureInfo.InvariantCulture))
            throw new ArgumentException("UF/ambiente da NFC-e diferem da configuração.");
        var offline = ide.TpEmis == "9";
        var version = (int)config.Geral.VersaoQRCode;
        if (version != 2 && version != 3) throw new NotSupportedException("QR Code deve ser versão 2 ou 3.");
        var input = nota.Chave + "|" + version + "|" + ambiente;
        var valor = dados.Total.ICMSTot.VNF.ToString("F2", CultureInfo.InvariantCulture);
        var dia = DateTimeOffset.Parse((string)ide.DhEmi, CultureInfo.InvariantCulture).Day
            .ToString("D2", CultureInfo.InvariantCulture);
        if (version == 2)
        {
            if (!int.TryParse(config.Geral.IdCSC, out var id) || id < 1 || id > 999999 ||
                string.IsNullOrWhiteSpace(config.Geral.CSC))
                throw new ArgumentException("Informe IdCSC e CSC válidos para QR Code 2.");
            if (offline)
            {
                var digest = nota.Signature?.SignedInfo?.Reference?.DigestValue;
                if (string.IsNullOrEmpty(digest))
                    throw new InvalidOperationException("Assinatura obrigatória para QR Code 2 offline.");
                var hex = Hex(Encoding.UTF8.GetBytes((string)digest));
                if (config.WebServices.UF.ToString() is "SP" or "PR" or "MS") hex = hex.ToLowerInvariant();
                input += "|" + dia + "|" + valor + "|" + hex;
            }

            input += "|" + id.ToString(CultureInfo.InvariantCulture);
            using var sha = SHA1.Create();
            input += "|" + Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(input + config.Geral.CSC)));
        }
        else if (offline)
        {
            var dest = dados.Dest;
            var doc = dest?.CNPJ ?? "";
            var tipo = "1";
            if (doc.Length == 0)
            {
                doc = dest?.CPF ?? "";
                tipo = "2";
            }

            if (doc.Length == 0)
            {
                doc = dest?.IdEstrangeiro ?? "";
                tipo = doc.Length == 0 ? "" : "3";
            }

            input += "|" + dia + "|" + valor + "|" + tipo + "|" + doc;
            using var rsa = config.Certificado?.GetRSAPrivateKey() ??
                            throw new ArgumentException("Certificado RSA obrigatório para QR Code 3 offline.");
            input += "|" + Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(input), HashAlgorithmName.SHA1,
                RSASignaturePadding.Pkcs1));
        }

        var url = NFeServiceManager.UrlQrCode(config);
        return url + (url.IndexOf("?p=", StringComparison.OrdinalIgnoreCase) >= 0 ? "" :
            url.EndsWith("?", StringComparison.Ordinal) ? "p=" :
            url.Contains("?") ? "&p=" : "?p=") + input;
    }
}