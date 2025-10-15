# Network Level Authentication Error

## Network Level Authentication Error 
<!--issueDescription-->
We investigated and identified that a network-level authentication (NLA) error occurred on this virtual machine <!--$vmname-->[vmname]<!--/$vmname-->. To regain RDP connectivity, reconfigure the RDP listener through the serial console, or other remote management options, as described in the following documentation.
<!--/issueDescription-->

### Recommended Steps

Use the `DisableNLA` script from [Run Command](https://docs.microsoft.com/azure/virtual-machines/windows/run-command) to temporarily disable NLA and regain access to the VM.

If the `DisableNLA` script does not enable you to regain access to the VM, go to [Troubleshoot authentication errors when you use RDP to connect to Azure VM](https://docs.microsoft.com/troubleshoot/azure/virtual-machines/cannot-connect-rdp-azure-vm) for more information.
