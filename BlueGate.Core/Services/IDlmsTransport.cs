using System;
using System.Threading.Tasks;
using Gurux.DLMS.Client;

namespace BlueGate.Core.Services
{
    public interface IDlmsTransport : IDisposable
    {
        bool IsOpen { get; }
        GXDLMSClient Client { get; }
        Task ConnectAsync();
        Task DisconnectAsync();
    }
}
