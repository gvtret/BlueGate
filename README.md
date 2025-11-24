# BlueGate
**Cross-platform DLMS ⇄ OPC UA Data Gateway (C#, .NET 9)**

## Overview
BlueGate is a high-performance, cross-platform industrial data gateway that bridges DLMS/COSEM smart metering protocols and OPC UA industrial automation systems.

---

## 🚀 Features
- Bi-directional DLMS ⇄ OPC UA data synchronization.
- Configurable mapping engine (OBIS ↔ OPC UA NodeIds).
- Modular service architecture (DlmsClientService, OpcUaServerService, ConversionEngine).
- Designed for .NET 9 and compatible with WSL2 / Linux / Windows.

---

## 🧱 Requirements
- .NET 9 SDK
- Visual Studio Code (with C# Dev Kit)
- Gurux.DLMS.Net SDK
- OPCFoundation.NetStandard.Opc.Ua

---

## ⚙️ Setup

```bash
# Clone the repository
git clone https://github.com/gvtret/BlueGate.git
cd BlueGate

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the server
dotnet run --project BlueGate.Server
```

---

## 🧩 Project Structure
```
BlueGate/
├── BlueGate.Core/
│   ├── Models/
│   ├── Services/
│   └── Config/
├── BlueGate.Server/
├── BlueGate.Tests/
└── README.md
```

---

## 🧪 Testing
```bash
dotnet test
```

---

## 📡 Next Steps
- Integrate Gurux.DLMS SDK for real DLMS communication.
- Add OPC UA server support via OPCFoundation SDK.
- Build Docker image and CI/CD pipeline.

---

© 2025 BlueGate Project. MIT License.
