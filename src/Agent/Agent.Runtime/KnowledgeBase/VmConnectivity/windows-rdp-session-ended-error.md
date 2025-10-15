# Your RDP Session has ended
<!--issueDescription-->
The RDP session to virtual machine (VM) <!--$vmname-->[vmname]<!--/$vmname--> with the following error message:

```
Your Remote Desktop Services session has ended, possibly for one of the following reasons: 
The administrator has ended the session. 
An error occurred while the connection was being established. 
A network problem occurred.
```

This error is occurs when Microsoft Direct3D9 (D3D9) has stopped working when using Microsoft Remote Desktop. The November cumulative update (KB5020032) resolved this issue. 

<!--/issueDescription-->

## **Recommended Steps**
Before proceeding with any of the solutions in this document, back up your VM OS disk by taking a [snapshot](https://learn.microsoft.com/en-us/azure/virtual-machines/snapshot-copy-managed-disk?tabs=portal). If you need to revert any changes made while troubleshooting, you can use the snapshot to recreate the disk.

Use the following steps to update your machine:
1. Follow this article to attach the OS disk to a repair VM: [Troubleshoot a Windows VM by attaching the OS disk to a repair VM through the Azure portal](https://learn.microsoft.com/en-us/troubleshoot/azure/virtual-machines/troubleshoot-recovery-disks-portal-windows).
2. Once attached, download the [November Update](https://support.microsoft.com/en-us/topic/november-22-2022-non-security-update-kb5020032-96bc003c-a310-445a-af1c-98e977a1a7d3) and save it. 
3. Copy the file to a location of your choice on the attached data disk. Remember this location as it will be used again in later steps.
4. Detach the OS disk from the repair VM and reattach to the affected VM.
5. Access the VM via Serial Console: [Azure Serial Console for Windows](https://learn.microsoft.com/en-us/troubleshoot/azure/virtual-machines/serial-console-windows).
6. Use Windows Update Standalone Installer to install the .msu file found in step 3. Instructions on how to do this can be found here: [Description of the Windows Update Standalone Installer in Windows](https://support.microsoft.com/en-us/topic/description-of-the-windows-update-standalone-installer-in-windows-799ba3df-ec7e-b05e-ee13-1cdae8f23b19).
