using System.IO;
using System.Text;
using OpenAC.Net.Devices;

namespace OpenAC.Net.NFe.DANFe.EscPos.Common;

internal sealed class CupomMemoriaStream(CupomMemoriaConfig configuracao) : OpenDeviceStream<CupomMemoriaConfig>(configuracao)
{
    protected override int Available => 0;
    protected override bool OpenInternal()
    {
        if (Writer != null) return false;
        Writer = new BinaryWriter(Config.Saida, Encoding.UTF8, true);
        return true;
    }
    protected override bool CloseInternal()
    {
        if (Writer == null) return false;
        Writer.Dispose(); Writer = null;
        return true;
    }
    protected override void DisposeManaged()
    {
        CloseInternal();
        base.DisposeManaged();
    }
}
