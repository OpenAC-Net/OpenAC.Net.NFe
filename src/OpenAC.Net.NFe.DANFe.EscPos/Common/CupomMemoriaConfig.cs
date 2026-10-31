using System.IO;
using OpenAC.Net.Devices;

namespace OpenAC.Net.NFe.DANFe.EscPos.Common;

// Adapta apenas a saída do componente; não conhece comandos ESC/POS.
internal sealed class CupomMemoriaConfig : BaseConfig
{
    static CupomMemoriaConfig() => OpenDeviceFactory.Register<CupomMemoriaConfig, CupomMemoriaStream>();

    public CupomMemoriaConfig(MemoryStream saida) : base("NFCeEscPosMemoria")
    {
        Saida = saida;
        ControlePorta = false;
    }

    internal MemoryStream Saida { get; }
}