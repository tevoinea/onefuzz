﻿using Azure.Core;
using Azure.ResourceManager.Compute;

namespace Microsoft.OneFuzz.Service {
    public class VMExtenionWrapper {
        public AzureLocation? Location { get; init; }
        public string? Name { get; init; }
        public string? TypePropertiesType { get; init; }
        public string? Publisher { get; init; }
        public string? TypeHandlerVersion { get; init; }
        public string? ForceUpdateTag {get;init;}
        public bool? AutoUpgradeMinorVersion { get; init; }
        public bool? EnableAutomaticUpgrade {get; init; }
        public BinaryData? Settings { get; init; }
        public BinaryData? ProtectedSettings { get; init; }

        public (string, VirtualMachineExtensionData) GetAsVirtualMachineExtension() {
            if (Location == null) { // EnsureNotNull does not satisfy the nullability checker
                throw new ArgumentNullException("Location required for VirtualMachineExtension");
            }
            TypePropertiesType.EnsureNotNull("TypePropertiesType required for VirtualMachineExtension");
            Publisher.EnsureNotNull("Publisher required for VirtualMachineExtension");
            TypeHandlerVersion.EnsureNotNull("TypeHandlerVersion required for VirtualMachineExtension");
            AutoUpgradeMinorVersion.EnsureNotNull("AutoUpgradeMinorVersion required for VirtualMachineExtension");
            Settings.EnsureNotNull("Settings required for VirtualMachineExtension");
            ProtectedSettings.EnsureNotNull("ProtectedSettings required for VirtualMachineExtension");
            ForceUpdateTag.EnsureNotNull("ForceUpdateTag required for VirtualMachineExtension");

            return (Name!, new VirtualMachineExtensionData(Location.Value) {
                TypePropertiesType = TypeHandlerVersion,
                Publisher = Publisher,
                TypeHandlerVersion = TypeHandlerVersion,
                AutoUpgradeMinorVersion = AutoUpgradeMinorVersion,
                EnableAutomaticUpgrade = EnableAutomaticUpgrade,
                ForceUpdateTag = ForceUpdateTag,
                Settings = Settings,
                ProtectedSettings = ProtectedSettings
            });
        }

        public VirtualMachineScaleSetExtensionData GetAsVirtualMachineScaleSetExtension() {
            Name.EnsureNotNull("Name required for VirtualMachineScaleSetExtension");
            TypePropertiesType.EnsureNotNull("TypePropertiesType required for VirtualMachineScaleSetExtension");
            Publisher.EnsureNotNull("Publisher required for VirtualMachineScaleSetExtension");
            TypeHandlerVersion.EnsureNotNull("TypeHandlerVersion required for VirtualMachineScaleSetExtension");
            AutoUpgradeMinorVersion.EnsureNotNull("AutoUpgradeMinorVersion required for VirtualMachineScaleSetExtension");
            Settings.EnsureNotNull("Settings required for VirtualMachineScaleSetExtension");
            ProtectedSettings.EnsureNotNull("ProtectedSettings required for VirtualMachineScaleSetExtension");
            return new VirtualMachineScaleSetExtensionData() {
                Name = Name,
                TypePropertiesType = TypeHandlerVersion,
                Publisher = Publisher,
                TypeHandlerVersion = TypeHandlerVersion,
                AutoUpgradeMinorVersion = AutoUpgradeMinorVersion,
                EnableAutomaticUpgrade = EnableAutomaticUpgrade,
                Settings = Settings,
                ProtectedSettings = ProtectedSettings
            };
        }
    }

}

