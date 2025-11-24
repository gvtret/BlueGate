using System;
using System.Collections.Generic;
using System.Linq;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace BlueGate.Core.Services;

public class BlueGateNodeManager : CustomNodeManager2
{
    private readonly MappingService _mappingService;

    public const string NamespaceUri = "urn:bluegate:opcua:nodes";

    public BlueGateNodeManager(IServerInternal server, ApplicationConfiguration configuration, MappingService mappingService)
        : base(server, configuration, new[] { NamespaceUri })
    {
        _mappingService = mappingService;
        SystemContext.NodeIdFactory = this;
    }

    public ushort ServerNamespaceIndex => NamespaceIndexes.FirstOrDefault();

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        base.CreateAddressSpace(externalReferences);

        var folderId = new NodeId("BlueGate", ServerNamespaceIndex);
        var folder = CreateFolder(null, folderId, "BlueGate", "BlueGate", externalReferences);
        AddPredefinedNode(SystemContext, folder);

        foreach (var profile in _mappingService.GetProfiles())
        {
            var parsedNodeId = NodeId.Parse(profile.OpcNodeId);
            var nodeId = parsedNodeId.NamespaceIndex == ServerNamespaceIndex
                ? parsedNodeId
                : new NodeId(parsedNodeId.Identifier, ServerNamespaceIndex);

            var identifier = parsedNodeId.Identifier?.ToString() ?? profile.OpcNodeId;
            var variable = new BaseDataVariableState(folder)
            {
                NodeId = nodeId,
                BrowseName = new QualifiedName(identifier, ServerNamespaceIndex),
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

            AddPredefinedNode(SystemContext, variable);
        }
    }

    private FolderState CreateFolder(NodeState? parent, NodeId nodeId, string browseName, string displayName, IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        var folder = new FolderState(parent)
        {
            NodeId = nodeId,
            BrowseName = new QualifiedName(browseName, ServerNamespaceIndex),
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
}
