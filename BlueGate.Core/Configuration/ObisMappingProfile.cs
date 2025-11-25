using System.ComponentModel.DataAnnotations;
using Gurux.DLMS.Enums;
using Opc.Ua;

namespace BlueGate.Core.Configuration
{
    public class ObisMappingProfile
    {
        [Required]
        public string ObisCode { get; set; }
        public string OpcNodeId { get; set; }
        public ObjectType ObjectType { get; set; } = ObjectType.Data;
        public int AttributeIndex { get; set; }
        public BuiltInType BuiltInType { get; set; } = BuiltInType.Double;
        public object InitialValue { get; set; }
    }
}
