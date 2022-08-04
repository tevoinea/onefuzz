﻿using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;

namespace Microsoft.OneFuzz.Service;

public interface IIpOperations {
    public Async.Task<NetworkInterfaceResource?> GetPublicNic(string resourceGroup, string name);

    public Async.Task<OneFuzzResultVoid> CreatePublicNic(string resourceGroup, string name, string region, Nsg? nsg);

    public Async.Task<string?> GetPublicIp(string resourceId);

    public Async.Task<PublicIPAddressResource?> GetIp(string resourceGroup, string name);

    public Async.Task DeleteNic(string resourceGroup, string name);

    public Async.Task DeleteIp(string resourceGroup, string name);

    public Async.Task CreateIp(string resourceGroup, string name, string region);
}

public class IpOperations : IIpOperations {
    private ILogTracer _logTracer;

    private IOnefuzzContext _context;

    public IpOperations(ILogTracer log, IOnefuzzContext context) {
        _logTracer = log;
        _context = context;
    }

    public async Async.Task<NetworkInterfaceResource?> GetPublicNic(string resourceGroup, string name) {
        _logTracer.Info($"getting nic: {resourceGroup} {name}");
        try {
            return await _context.Creds.GetResourceGroupResource().GetNetworkInterfaceAsync(name);
        } catch (RequestFailedException) {
            return null;
        }
    }

    public async Async.Task<PublicIPAddressResource?> GetIp(string resourceGroup, string name) {
        _logTracer.Info($"getting ip {resourceGroup}:{name}");

        try {
            return await _context.Creds.GetResourceGroupResource().GetPublicIPAddressAsync(name);
        } catch (RequestFailedException) {
            return null;
        }
    }

    public async System.Threading.Tasks.Task DeleteNic(string resourceGroup, string name) {
        _logTracer.Info($"deleting nic {resourceGroup}:{name}");
        await _context.Creds.GetResourceGroupResource().GetNetworkInterfaceAsync(name).Result.Value.DeleteAsync(WaitUntil.Started);
    }

    public async System.Threading.Tasks.Task DeleteIp(string resourceGroup, string name) {
        _logTracer.Info($"deleting ip {resourceGroup}:{name}");
        await _context.Creds.GetResourceGroupResource().GetPublicIPAddressAsync(name).Result.Value.DeleteAsync(WaitUntil.Started);
    }

    public async Task<string?> GetPublicIp(string resourceId) {
        // TODO: Parts of this function seem redundant, but I'm mirroring
        // the python code exactly. We should revisit this.
        _logTracer.Info($"getting ip for {resourceId}");
        var resource = await (_context.Creds.GetData(_context.Creds.ParseResourceId(resourceId)));
        var networkInterfaces = await _context.Creds.GetResourceGroupResource().GetNetworkInterfaceAsync(
            resource.Data.Name
        );
        var publicIpConfigResource = (await networkInterfaces.Value.GetNetworkInterfaceIPConfigurations().FirstAsync());
        publicIpConfigResource = await publicIpConfigResource.GetAsync();
        var publicIp = publicIpConfigResource.Data.PublicIPAddress;
        if (publicIp == null) {
            return null;
        }
        resource = _context.Creds.ParseResourceId(publicIp.Id);
        try {
            resource = await _context.Creds.GetData(resource);
            var publicIpResource = await _context.Creds.GetResourceGroupResource().GetPublicIPAddressAsync(
                resource.Data.Name
            );
            return publicIpResource.Value.Data.IPAddress;
        } catch (RequestFailedException) {
            return null;
        }
    }

    public async Task<OneFuzzResultVoid> CreatePublicNic(string resourceGroup, string name, string region, Nsg? nsg) {
        _logTracer.Info($"creating nic for {resourceGroup}:{name} in {region}");

        var network = await Network.Create(region, _context);
        var subnetId = await network.GetId();

        if (string.IsNullOrEmpty(subnetId)) {
            await network.Create();
            return OneFuzzResultVoid.Ok;
        }

        if (nsg != null) {
            var subnet = await network.GetSubnet();
            if (subnet != null && subnet.Data.NetworkSecurityGroup == null) {
                var vnet = await network.GetVnet();
                var result = await _context.NsgOperations.AssociateSubnet(nsg.Name, vnet!, subnet);
                if (!result.IsOk) {
                    return OneFuzzResultVoid.Error(result.ErrorV);
                }
                return OneFuzzResultVoid.Ok;
            }
        }

        var ip = await GetIp(resourceGroup, name);
        if (ip == null) {
            await CreateIp(resourceGroup, name, region);
            return OneFuzzResultVoid.Ok;
        }

        var networkInterface = new NetworkInterfaceData {
            Location = region,
        };

        networkInterface.IPConfigurations.Add(new NetworkInterfaceIPConfigurationData {
            Name = "myIPConfig",
            PublicIPAddress = ip?.Data,
            Subnet = new SubnetData {
                Id = subnetId
            }
        }
        );

        var onefuzzOwner = _context.ServiceConfiguration.OneFuzzOwner;
        if (!string.IsNullOrEmpty(onefuzzOwner)) {
            if (!networkInterface.Tags.TryAdd("OWNER", onefuzzOwner)) {
                _logTracer.Warning($"Failed to add tag 'OWNER':{onefuzzOwner} to nic {resourceGroup}:{name}");
            }
        }

        try {
            await _context.Creds.GetResourceGroupResource().GetNetworkInterfaces().CreateOrUpdateAsync(
                WaitUntil.Started,
                name,
                networkInterface
            );
        } catch (RequestFailedException ex) {
            if (!ex.ToString().Contains("RetryableError")) {
                return OneFuzzResultVoid.Error(
                    ErrorCode.VM_CREATE_FAILED,
                    $"unable to create nic: {ex}"
                );
            }
        }

        return OneFuzzResultVoid.Ok;
    }

    public async Async.Task CreateIp(string resourceGroup, string name, string region) {
        var ipParams = new PublicIPAddressData() {
            Location = region,
            PublicIPAllocationMethod = IPAllocationMethod.Dynamic
        };

        var onefuzzOwner = _context.ServiceConfiguration.OneFuzzOwner;
        if (!string.IsNullOrEmpty(onefuzzOwner)) {
            if (!ipParams.Tags.TryAdd("OWNER", onefuzzOwner)) {
                _logTracer.Warning($"Failed to add tag 'OWNER':{onefuzzOwner} to ip {resourceGroup}:{name}");
            }
        }

        await _context.Creds.GetResourceGroupResource().GetPublicIPAddresses().CreateOrUpdateAsync(
            WaitUntil.Started, name, ipParams
        );
        return;
    }
}
