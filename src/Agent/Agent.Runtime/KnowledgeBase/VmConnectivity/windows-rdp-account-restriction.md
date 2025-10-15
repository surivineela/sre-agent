# A user account restriction prevents logging on when connecting to an Azure Windows VM
<!--issueDescription-->
We determined that your Windows virtual machine (VM) <!--$vmname-->[vmname]<!--/$vmname--> is in an inaccessible state due to an error, "A user account restriction is preventing you from logging on". 

When you try to connect to an Azure Windows VM by using Remote Desktop Protocol (RDP), you receive the following error message on the login screen: 
"A user account restriction (for example, a time-of-day restriction) is preventing you from logging on. For assistance, contact your system administrator or technical support."

The account credentials you used might not have the needed permissions associated with the VM.
<!--/issueDescription-->

## Use different credentials or reset account password

If you have more than one set of credentials, try logging in with one of those accounts, or you can reset the account password. Select the following button to reset the VM password.

[Reset VM Password](button-data-context:microsoft_azure_compute.VirtualMachinePasswordReset.id.$resourceId)

Learn more: [Reset Remote Desktop Services or its administrator password in a Windows VM](https://docs.microsoft.com/troubleshoot/azure/virtual-machines/reset-rdp).

## Resources

[Troubleshoot Remote Desktop connections to an Azure virtual machine](https://docs.microsoft.com/azure/virtual-machines/troubleshooting/troubleshoot-rdp-connection)
