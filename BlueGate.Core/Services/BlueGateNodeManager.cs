using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace BlueGate.Core.Services;

public class BlueGateNodeManager : CustomNodeManager2
{
    private readonly object _addressSpaceLock = new();
    private readonly IServerInternal _server;
    private readonly MappingService _mappingService;
    private readonly DlmsClientService _dlmsClientService;
    private readonly ILogger<BlueGateNodeManager> _logger;
    private readonly List<NodeId> _managedNodeIds = new();
    private FolderState? _rootFolder;
    private ushort? _namespaceIndex;

    public const string NamespaceUri = "urn:bluegate:opcua:nodes";

    public BlueGateNodeManager(
        IServerInternal server,
        ApplicationConfiguration configuration,
        MappingService mappingService,
        DlmsClientService dlmsClientService,
        ILogger<BlueGateNodeManager> logger)
        : base(server, configuration, new[] { NamespaceUri })
    {
        _server = server;
        _mappingService = mappingService;
        _dlmsClientService = dlmsClientService;
        _logger = logger;
        SystemContext.NodeIdFactory = this;

        _mappingService.ProfilesChanged += OnProfilesChanged;
    }

    public ushort ServerNamespaceIndex => NamespaceIndexes.FirstOrDefault();

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        base.CreateAddressSpace(externalReferences);

        lock (_addressSpaceLock)
        {
            _namespaceIndex ??= ServerNamespaceIndex;

            var folderId = new NodeId("BlueGate", _namespaceIndex.Value);
            _rootFolder = CreateFolder(null, folderId, "BlueGate", "BlueGate", externalReferences);
            AddPredefinedNode(SystemContext, _rootFolder);

            RebuildNodes(_server.DefaultSystemContext);
        }
    }

    private ServiceResult OnWriteValue(ISystemContext context, NodeState node, ref object value)
    {
        if (node.NodeId is null)
            return StatusCodes.BadNodeIdUnknown;

        if (node is not BaseVariableState variable)
        {
            _logger.LogWarning("Write rejected for unsupported node type {NodeId}", node.NodeId);
            return StatusCodes.BadNodeIdInvalid;
        }

        var obisCode = _mappingService.MapToDlms(node.NodeId);
        if (string.IsNullOrWhiteSpace(obisCode))
        {
            _logger.LogWarning("Write rejected for unmapped node {NodeId}", node.NodeId);
            return StatusCodes.BadNodeIdUnknown;
        }

        try
        {
            variable.Value = value;
            variable.Timestamp = DateTime.UtcNow;
            variable.ClearChangeMasks(context, false);

            _dlmsClientService.WriteAsync(obisCode, value).GetAwaiter().GetResult();

            return ServiceResult.Good;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DLMS write failed for node {NodeId} mapped to OBIS {ObisCode}", node.NodeId, obisCode);
            return new ServiceResult(StatusCodes.BadUnexpectedError, ex.Message);
        }
    }

    private FolderState CreateFolder(NodeState? parent, NodeId nodeId, string browseName, string displayName, IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        var folder = new FolderState(parent)
        {
            NodeId = nodeId,
            BrowseName = new QualifiedName(browseName, _namespaceIndex ?? ServerNamespaceIndex),
            DisplayName = new LocalizedText(displayName),
            TypeDefinitionId = ObjectTypeIds.FolderType,
            EventNotifier = EventNotifiers.None
        };

        if (parent == null)
        {
            folder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
            AddRootReference(externalReferences, ObjectIds.ObjectsFolder, ReferenceTypeIds.Organizes, false, folder.NodeId);
        }
        else
        {
            parent.AddReference(ReferenceTypeIds.Organizes, false, folder.NodeId);
            folder.AddReference(ReferenceTypeIds.Organizes, true, parent.NodeId);
        }

        return folder;
    }

    private static void AddRootReference(IDictionary<NodeId, IList<IReference>> externalReferences, NodeId sourceId, NodeId referenceTypeId, bool isInverse, NodeId targetId)
    {
        if (!externalReferences.TryGetValue(sourceId, out var references))
        {
            references = new List<IReference>();
            externalReferences[sourceId] = references;
        }

        references.Add(new NodeStateReference(referenceTypeId, isInverse, targetId));
    }

    private void OnProfilesChanged(object? sender, EventArgs e)
    {
        if (!_server.IsRunning || _rootFolder == null)
            return;

        lock (_addressSpaceLock)
        {
            RebuildNodes(_server.DefaultSystemContext);
        }
    }

    private void RebuildNodes(ServerSystemContext context)
    {
        if (_rootFolder == null)
            return;

        foreach (var nodeId in _managedNodeIds)
        {
            DeleteNode(context, nodeId);
        }

        _managedNodeIds.Clear();

        foreach (var profile in _mappingService.GetProfiles())
        {
            var parsedNodeId = NodeId.Parse(profile.OpcNodeId);
            var namespaceIndex = _namespaceIndex ?? parsedNodeId.NamespaceIndex;
            var nodeId = parsedNodeId.NamespaceIndex == namespaceIndex
                ? parsedNodeId
                : new NodeId(parsedNodeId.Identifier, namespaceIndex);

            var identifier = parsedNodeId.Identifier?.ToString() ?? profile.OpcNodeId;
            var variable = new BaseDataVariableState(_rootFolder)
            {
                NodeId = nodeId,
                BrowseName = new QualifiedName(identifier, namespaceIndex),
                DisplayName = new LocalizedText(identifier),
                Description = new LocalizedText($"DLMS OBIS: {profile.ObisCode}"),
                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                DataType = DataTypeIds.BaseDataType,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.CurrentReadOrWrite,
                UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.UtcNow
            };

            variable.OnSimpleWriteValue = OnWriteValue;

            AddPredefinedNode(context, variable);
            _managedNodeIds.Add(variable.NodeId);
        }
    }
}
