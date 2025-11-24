using System.Collections.Generic;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace BlueGate.Core.Services;

public class BlueGateServer : StandardServer
{
    private readonly MappingService _mappingService;

    public BlueGateServer(MappingService mappingService)
    {
        _mappingService = mappingService;
    }

    public BlueGateNodeManager? NodeManager { get; private set; }

    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        NodeManager = new BlueGateNodeManager(server, configuration, _mappingService);

        var nodeManagers = new List<INodeManager> { NodeManager };
        return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
    }
}
