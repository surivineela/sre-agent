# Remote Desktop license server isn't available when you connect to an Azure VM

<!--issueDescription-->
We have determined that your Windows Virtual Machine (VM) <!--$vmname-->[vmname]<!--/$vmname--> is in an inaccessible state because the Remote Desktop license server isn't available when you connect to an Azure VM.
<!--/issueDescription-->

In this scenario, you receive one of the following error messages:

```
The remote session was disconnected because there are no Remote Desktop license servers available to provide a license.
```

```
No Remote Desktop license server is available. Remote Desktop Services will stop working because this computer is past its grace period and hasn't contacted at least one valid Windows Server 2008 license server. Select this message to open RD Session Host Server Configuration to use Licensing Diagnosis.
```

## Recommended steps

You might be able to connect to the VM using an Admin session, open the [VM Overview](data-blade:Microsoft_Azure_Compute.VirtualMachineProtoBlade.id.$resourceId;data-blade-uri:{$domain}/#blade/Microsoft_Azure_Compute/VirtualMachineProtoBlade/id/{$resourceId}) and then select **Connect**. Select **Download RDP File** for a connection file that has the Admin session flag. After you connect to the VM, use the guidance in the following link to resolve the licensing issue.

Learn more: [Remote Desktop license server isn't available when you connect to an Azure VM](https://docs.microsoft.com/troubleshoot/azure/virtual-machines/troubleshoot-rdp-no-license-server)

### Resources

[Troubleshoot Remote Desktop connections to an Azure virtual machine](https://docs.microsoft.com/azure/virtual-machines/troubleshooting/troubleshoot-rdp-connection)
