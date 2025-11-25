using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Gurux.DLMS;
using Gurux.DLMS.Enums;

namespace BlueGate.Core.Configuration
{

    public class DlmsClientOptions
    {
        [Required]
        public string Host { get; set; } = "localhost";

        [Range(1, 65535)]
        public int Port { get; set; } = 4059;

        public int ClientAddress { get; set; } = 16;
        public int ServerAddress { get; set; } = 1;

        public Authentication Authentication { get; set; } = Authentication.None;
        public string Password { get; set; }
        public Security Security { get; set; } = Security.None;
        public SecuritySuite SecuritySuite { get; set; } = SecuritySuite.None;
        public string BlockCipherKey { get; set; }
        public string AuthenticationKey { get; set; }
        public string SystemTitle { get; set; }
        public long? InvocationCounter { get; set; }
        public string InvocationCounterPath { get; set; } = "invocationCounter.txt";
        public Gurux.Communication.InterfaceType InterfaceType { get; set; } = Gurux.Communication.InterfaceType.HDLC;
        public int WaitTime { get; set; } = 5000;
        public int ReceiveCount { get; set; }
        public List<ObisMappingProfile> Profiles { get; set; } = new();
    }
}
