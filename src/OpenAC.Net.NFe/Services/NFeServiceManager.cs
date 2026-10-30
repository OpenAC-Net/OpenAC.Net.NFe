using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.DFe.Core.Service;
using OpenAC.Net.NFe.Common;
using OpenAC.Net.NFe.Common.Types;

namespace OpenAC.Net.NFe.Services;

/// <summary>Catálogo XML compartilhado com o Core, separado por modelo, versão, emissão, UF e ambiente.</summary>
public static class NFeServiceManager
{
    internal static DFeServices<TipoServicoNFe> Padrao { get; } = CarregarPadrao();
    private static DFeServices<TipoServicoNFe> CarregarPadrao()
    {
        using var stream = typeof(NFeServiceManager).Assembly.GetManifestResourceStream("OpenAC.Net.NFe.Resources.services.xml");
        return CarregarCatalogo(stream ?? throw new InvalidOperationException("Catálogo SEFAZ ausente."));
    }

    /// <summary>Valida a estrutura e desserializa pelo ReadXml gerado pelo DFe.Core.</summary>
    public static DFeServices<TipoServicoNFe> CarregarCatalogo(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        var document = XDocument.Load(stream);
        XNamespace ns = "https://www.openac.net.br/";
        if (document.Root?.Name != ns + "DFeServices") throw new ArgumentException("Raiz do catálogo DFeServices inválida.");
        var profiles = new HashSet<string>();
        foreach (var group in document.Root.Elements())
        {
            var model = (string)group.Attribute("Tipo");
            var emission = (string)group.Attribute("TipoEmissao");
            if (group.Name != ns + "Services" || model is not ("NFe" or "NFCe") || (string)group.Attribute("Versao") != "4.00")
                throw new NotSupportedException("O catálogo de comunicação aceita apenas NF-e/NFC-e 4.00.");
            if (emission is not ("1" or "6" or "7") || model == "NFCe" && emission != "1" || !profiles.Add(model + ":" + emission))
                throw new ArgumentException("Modelo/perfil de emissão inválido ou duplicado no catálogo.");
            var environments = new HashSet<string>();
            foreach (var environment in group.Elements())
            {
                var type = (string)environment.Attribute("Tipo");
                var state = (string)environment.Attribute("UF");
                if (environment.Name != ns + "Ambiente" || type is not ("1" or "2") ||
                    !Enum.TryParse(state, out DFeSiglaUF uf) || !EstadoValido(uf) || state != uf.ToString() || !environments.Add(type + ":" + state))
                    throw new ArgumentException("Ambiente/UF inválido ou duplicado no catálogo.");
                var addresses = environment.Elements(ns + "Enderecos").ToArray();
                if (addresses.Length != 1 || environment.Elements().Count() != 1) throw new ArgumentException("Informe um grupo Enderecos por ambiente.");
                var services = new HashSet<TipoServicoNFe>();
                foreach (var address in addresses[0].Elements())
                {
                    var name = (string)address.Attribute("Tipo");
                    if (address.Name != ns + "Endereco" || !Enum.TryParse(name, out TipoServicoNFe service) || !Enum.IsDefined(typeof(TipoServicoNFe), service) || name != service.ToString() || !services.Add(service))
                        throw new NotSupportedException("Serviço inválido ou duplicado no catálogo: " + name);
                    address.Value = NormalizarEndereco(service, address.Value);
                }
            }
        }
        if (profiles.Count == 0) throw new ArgumentException("Catálogo de serviços vazio.");
        var catalog = new DFeServices<TipoServicoNFe>();
        catalog.ReadXml(document.Root);
        return catalog;
    }

    public static string ObterEndereco(NFeConfig config, TipoServicoNFe service)
    {
        ValidarConsulta(config, service);
        if (config.WebServices.Enderecos.TryGetValue(service, out var address)) return NormalizarEndereco(service, address);
        var catalog = config.Arquivos.Catalogo ?? Padrao;
        var emission = Perfil(config, service);
        var type = service == TipoServicoNFe.NfeConsultaCadastro || service is TipoServicoNFe.RecepcaoEventoAN or TipoServicoNFe.NFeDistribuicaoDFe
            ? DFeTipoServico.NFe : config.Geral.Modelo == ModeloNFe.NFCe ? DFeTipoServico.NFCe : DFeTipoServico.NFe;
        var profile = catalog.Webservices.SingleOrDefault(item => item.Tipo == type && item.TipoEmissao == emission && item.Versao == "4.00");
        var environment = profile?[config.WebServices.Ambiente, config.WebServices.UF];
        if (environment == null || !environment.Enderecos.TryGetValue(service, out address))
            throw new NotSupportedException($"Serviço {service} não disponível para {type}, {config.WebServices.UF}, {config.WebServices.Ambiente}, emissão {emission}.");
        return address;
    }

    public static string NormalizarEndereco(TipoServicoNFe service, string address)
    {
        var value = address?.Trim();
        var publicUrl = UrlPublica(service);
        // As URLs públicas por chave do catálogo original podem não conter esquema.
        if (service is TipoServicoNFe.ConsultaChave or TipoServicoNFe.ConsultaChave2 or TipoServicoNFe.ConsultaChave3 && !string.IsNullOrWhiteSpace(value) && !value.Contains(":") && !value.Contains("@") &&
            Uri.TryCreate("https://" + value, UriKind.Absolute, out var publicAddress) && publicAddress.Host.Contains(".")) return value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps && !(publicUrl && uri.Scheme == Uri.UriSchemeHttp))
            throw new ArgumentException("Serviços SOAP exigem HTTPS; URLs públicas devem usar HTTP ou HTTPS.", nameof(address));
        if (!publicUrl && value.EndsWith("?wsdl", StringComparison.OrdinalIgnoreCase)) return value.Substring(0, value.Length - 5);
        if (!publicUrl && value.EndsWith("?wsdl=", StringComparison.OrdinalIgnoreCase)) return value.Substring(0, value.Length - 6);
        return value;
    }

    public static void ValidarConsulta(NFeConfig config, TipoServicoNFe service)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (config.Geral.VersaoDFe != NFeVersao.v400) throw new NotSupportedException("Comunicação exige NF-e/NFC-e 4.00.");
        if (config.Geral.Modelo is not (ModeloNFe.NFe or ModeloNFe.NFCe)) throw new ArgumentException("Modelo deve ser 55 ou 65.");
        if (!EstadoValido(config.WebServices.UF)) throw new ArgumentOutOfRangeException(nameof(config), "UF inválida.");
        if (config.WebServices.Ambiente is not (DFeTipoAmbiente.Producao or DFeTipoAmbiente.Homologacao)) throw new ArgumentException("Ambiente deve ser produção ou homologação.");
        if (!Enum.IsDefined(typeof(TipoServicoNFe), service)) throw new ArgumentOutOfRangeException(nameof(service));
        if (service is TipoServicoNFe.NFeDistribuicaoDFe or TipoServicoNFe.RecepcaoEventoAN && config.Geral.Modelo != ModeloNFe.NFe)
            throw new NotSupportedException("Serviço nacional disponível somente para NF-e modelo 55.");
        var emission = config.Geral.FormaEmissao;
        if (!Enum.IsDefined(typeof(DFeTipoEmissao), emission)) throw new ArgumentOutOfRangeException(nameof(config), "Emissão inválida.");
        if (UrlPublica(service) || service is TipoServicoNFe.NfeConsultaCadastro or TipoServicoNFe.NFeDistribuicaoDFe or TipoServicoNFe.RecepcaoEventoAN) return;
        if (config.Geral.Modelo == ModeloNFe.NFCe && emission is not (DFeTipoEmissao.Normal or DFeTipoEmissao.OffLine))
            throw new NotSupportedException("Este cliente NFC-e atende emissão normal e offline.");
        if (config.Geral.Modelo == ModeloNFe.NFe && emission is not (DFeTipoEmissao.Normal or DFeTipoEmissao.Contingencia or DFeTipoEmissao.DPEC or DFeTipoEmissao.FSDA or DFeTipoEmissao.SVCAN or DFeTipoEmissao.SVCRS))
            throw new NotSupportedException("Modalidade de emissão não atendida pelo cliente NF-e.");
    }

    internal static DFeTipoEmissao Perfil(NFeConfig config, TipoServicoNFe service) =>
        !UrlPublica(service) && service is not (TipoServicoNFe.NfeConsultaCadastro or TipoServicoNFe.NFeDistribuicaoDFe or TipoServicoNFe.RecepcaoEventoAN)
        && config.Geral.FormaEmissao is DFeTipoEmissao.SVCAN or DFeTipoEmissao.SVCRS ? config.Geral.FormaEmissao : DFeTipoEmissao.Normal;

    private static bool UrlPublica(TipoServicoNFe service) => service is TipoServicoNFe.QRCode or TipoServicoNFe.QRCode2 or TipoServicoNFe.QRCode3
        or TipoServicoNFe.ConsultaChave or TipoServicoNFe.ConsultaChave2 or TipoServicoNFe.ConsultaChave3;

    private static bool EstadoValido(DFeSiglaUF uf) => Enum.IsDefined(typeof(DFeSiglaUF), uf) && uf is not (DFeSiglaUF.AN or DFeSiglaUF.EX or DFeSiglaUF.SU);
    public static string UrlQrCode(NFeConfig config) => ObterUrlPublica(config, TipoServicoNFe.QRCode2, TipoServicoNFe.QRCode3, TipoServicoNFe.QRCode);
    public static string UrlConsulta(NFeConfig config) => ObterUrlPublica(config, TipoServicoNFe.ConsultaChave2, TipoServicoNFe.ConsultaChave3, TipoServicoNFe.ConsultaChave);
    private static string ObterUrlPublica(NFeConfig config, TipoServicoNFe version2, TipoServicoNFe version3, TipoServicoNFe generic)
    {
        if (config.WebServices.Enderecos.TryGetValue(generic, out var address)) return NormalizarEndereco(generic, address);
        if (config.Geral.VersaoQRCode is not (NFeVersaoQRCode.v200 or NFeVersaoQRCode.v300)) throw new NotSupportedException("QR Code deve ser versão 2 ou 3.");
        return ObterEndereco(config, config.Geral.VersaoQRCode == NFeVersaoQRCode.v300 ? version3 : version2);
    }
}
