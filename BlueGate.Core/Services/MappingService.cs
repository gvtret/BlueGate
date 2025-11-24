using System;
using System.Collections.Generic;
using System.Linq;
using BlueGate.Core.Configuration;
using BlueGate.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;

namespace BlueGate.Core.Services;

public class MappingService
{
    private readonly object _lock = new();
    private readonly IOptionsMonitor<DlmsClientOptions> _optionsMonitor;
    private readonly ILogger<MappingService> _logger;
    private List<MappingProfile> _profiles;

    public event EventHandler? ProfilesChanged;

    public MappingService(IOptionsMonitor<DlmsClientOptions> optionsMonitor, ILogger<MappingService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _profiles = LoadProfiles(optionsMonitor.CurrentValue);

        _optionsMonitor.OnChange(options => UpdateProfiles(options));
    }

    public string? MapToOpcUa(string obisCode)
    {
        var profiles = GetProfiles();
        return profiles.FirstOrDefault(m => m.ObisCode == obisCode)?.OpcNodeId;
    }

    public string? MapToDlms(string nodeId)
    {
        try
        {
            var parsedNodeId = NodeId.Parse(nodeId);
            return MapToDlms(parsedNodeId);
        }
        catch (Exception ex) when (ex is ServiceResultException or FormatException)
        {
            return null;
        }
    }

    public string? MapToDlms(NodeId nodeId)
    {
        var identifier = nodeId?.Identifier?.ToString();

        if (identifier is null)
            return null;

        foreach (var profile in GetProfiles())
        {
            try
            {
                var mappedNodeId = NodeId.Parse(profile.OpcNodeId);
                if (mappedNodeId.Identifier?.ToString() == identifier)
                    return profile.ObisCode;
            }
            catch (Exception ex) when (ex is ServiceResultException or FormatException)
            {
                continue;
            }
        }

        return null;
    }

    public IReadOnlyCollection<MappingProfile> GetProfiles()
    {
        lock (_lock)
        {
            return _profiles.AsReadOnly();
        }
    }

    private void UpdateProfiles(DlmsClientOptions options)
    {
        var updatedProfiles = LoadProfiles(options);

        lock (_lock)
        {
            _profiles = updatedProfiles;
        }

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    private List<MappingProfile> LoadProfiles(DlmsClientOptions options)
    {
        var profiles = options.Profiles ?? new List<MappingProfile>();

        var validProfiles = profiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.ObisCode)
                              && !string.IsNullOrWhiteSpace(profile.OpcNodeId))
            .Select(profile => ApplyDefaults(profile))
            .Where(profile => profile is not null)
            .Cast<MappingProfile>()
            .ToList();

        if (validProfiles.Count == 0)
        {
            _logger.LogInformation(
                "No mapping profiles configured or all were invalid. Applying defaults for DLMS to OPC UA mapping.");
            validProfiles.AddRange(DlmsClientOptions.DefaultProfiles);
            foreach (var profile in validProfiles)
            {
                MappingProfileDefaults.EnsureDefaults(profile);
            }
        }

        return validProfiles;
    }

    private MappingProfile? ApplyDefaults(MappingProfile profile)
    {
        if (!MappingProfileDefaults.EnsureDefaults(profile))
        {
            _logger.LogWarning("Profile for OBIS {ObisCode} has no supported OPC UA data type configured and will be ignored.",
                profile.ObisCode);
            return null;
        }

        return profile;
    }
}
