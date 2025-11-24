using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace BlueGate.Core.Services;

public class BlueGateServer : StandardServer
{
    private readonly MappingService _mappingService;
    private readonly DlmsClientService _dlmsClientService;
    private readonly ILoggerFactory _loggerFactory;

    public BlueGateServer(MappingService mappingService, DlmsClientService dlmsClientService, ILoggerFactory loggerFactory)
    {
        _mappingService = mappingService;
        _dlmsClientService = dlmsClientService;
        _loggerFactory = loggerFactory;
    }

    public BlueGateNodeManager? NodeManager { get; private set; }

    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        var logger = _loggerFactory.CreateLogger<BlueGateNodeManager>();
        NodeManager = new BlueGateNodeManager(server, configuration, _mappingService, _dlmsClientService, logger);

        var nodeManagers = new List<INodeManager> { NodeManager };
        return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
    }
}
