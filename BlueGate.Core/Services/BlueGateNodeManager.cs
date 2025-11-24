using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Server;

namespace BlueGate.Core.Services
{
    public class BlueGateNodeManager : CustomNodeManager2
    {
        private readonly ILogger<BlueGateNodeManager> _logger;
        private readonly Dictionary<string, DataVariableState> _nodes = new Dictionary<string, DataVariableState>();
        private readonly Dictionary<string, ObisMappingProfile> _opcNodeIdToProfileMap = new Dictionary<string, ObisMappingProfile>();
        private readonly DlmsClientService _dlmsClient;
        private readonly MappingService _mappingService;

        public BlueGateNodeManager(IServerInternal server, ApplicationConfiguration configuration, DlmsClientService dlmsClient, MappingService mappingService, ILogger<BlueGateNodeManager> logger, string namespaceUri)
            : base(server, configuration, namespaceUri)
        {
            _logger = logger;
            _dlmsClient = dlmsClient;
            _mappingService = mappingService;
        }

        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                base.CreateAddressSpace(externalReferences);
                RebuildAddressSpace();
            }
        }

        public virtual void RebuildAddressSpace()
        {
            lock (Lock)
            {
                foreach (var node in _nodes.Values)
                {
                    RemovePredefinedNode(SystemContext, node, false);
                }
                _nodes.Clear();
                _opcNodeIdToProfileMap.Clear();

                foreach (var profile in _mappingService.GetObisProfiles())
                {
                    _opcNodeIdToProfileMap[profile.OpcNodeId] = profile;
                    CreateVariable(profile.OpcNodeId, profile.InitialValue, profile.BuiltInType);
                }
            }
        }

        public void UpdateNodeValue(string nodeId, object value, BuiltInType builtInType)
        {
            lock (Lock)
            {
                if (_nodes.TryGetValue(nodeId, out var node))
                {
                    node.Value = value;
                    node.Timestamp = DateTime.UtcNow;
                    node.ClearChangeMasks(SystemContext, false);
                }
                else
                {
                    _logger.LogWarning("Node {NodeId} not found, creating new node.", nodeId);
                    CreateVariable(nodeId, value, builtInType);
                }
            }
        }

        private void CreateVariable(string nodeId, object initialValue, BuiltInType builtInType)
        {
            var variable = new DataVariableState(null)
            {
                SymbolicName = nodeId,
                ReferenceTypeId = ReferenceTypeIds.Organizes,
                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                NodeId = new NodeId(nodeId, NamespaceIndex),
                BrowseName = new QualifiedName(nodeId, NamespaceIndex),
                DisplayName = new LocalizedText("en", nodeId),
                Value = initialValue,
                DataType = GetDataTypeId(builtInType),
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.CurrentRead | AccessLevels.CurrentWrite,
                UserAccessLevel = AccessLevels.CurrentRead | AccessLevels.CurrentWrite,
                Timestamp = DateTime.UtcNow,
                OnWriteValue = OnWriteValue
            };

            AddPredefinedNode(SystemContext, variable);
            _nodes.Add(nodeId, variable);
        }

        private NodeId GetDataTypeId(BuiltInType builtInType)
        {
            switch (builtInType)
            {
                case BuiltInType.Boolean:
                    return DataTypeIds.Boolean;
                case BuiltInType.SByte:
                    return DataTypeIds.SByte;
                case BuiltInType.Byte:
                    return DataTypeIds.Byte;
                case BuiltInType.Int16:
                    return DataTypeIds.Int16;
                case BuiltInType.UInt16:
                    return DataTypeIds.UInt16;
                case BuiltInType.Int32:
                    return DataTypeIds.Int32;
                case BuiltInType.UInt32:
                    return DataTypeIds.UInt32;
                case BuiltInType.Int64:
                    return DataTypeIds.Int64;
                case BuiltInType.UInt64:
                    return DataTypeIds.UInt64;
                case BuiltInType.Float:
                    return DataTypeIds.Float;
                case BuiltInType.Double:
                    return DataTypeIds.Double;
                case BuiltInType.String:
                    return DataTypeIds.String;
                case BuiltInType.DateTime:
                    return DataTypeIds.DateTime;
                default:
                    return DataTypeIds.BaseDataType;
            }
        }

        private ServiceResult OnWriteValue(ISystemContext context, NodeState node, NumericRange indexRange, QualifiedName dataEncoding, ref object value, ref StatusCode statusCode, ref DateTime timestamp)
        {
            var variable = node as DataVariableState;
            var nodeId = variable.SymbolicName;

            if (!_opcNodeIdToProfileMap.TryGetValue(nodeId, out var profile))
            {
                return ServiceResult.Create(StatusCodes.BadNodeIdUnknown, "OBIS profile not found for this node.");
            }

            Task.Run(async () =>
            {
                try
                {
                    await _dlmsClient.WriteObjectAsync(profile, value);
                    variable.StatusCode = StatusCodes.Good;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write value to DLMS device for node {NodeId}", nodeId);
                    variable.StatusCode = StatusCodes.Bad;
                }
                variable.Timestamp = DateTime.UtcNow;
                variable.ClearChangeMasks(SystemContext, false);
            });

            return ServiceResult.Create(StatusCodes.Uncertain, "Write operation is in progress.");
        }
    }
}
