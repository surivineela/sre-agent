# Unable to RDP to a Windows Virtual Machine (VM) in Azure due to error 'We can't sign into your account'
<!--issueDescription-->
We have determined that your Windows Virtual Machine (VM) <!--$vmname-->[vmname]<!--/$vmname--> is in an inaccessible state due to an issue with the user profile.

```
We can't sign into your account
This problem can often be fixed by signing out of your account and then signing back in.
If you don't sign out now, any files you create or changes you make will be lost.
```

<!--/issueDescription-->

## **Recommended Steps**

Before proceeding with any of the solutions in this document, back up your VM OS disk by taking a [snapshot](https://docs.microsoft.com/azure/virtual-machines/windows/snapshot-copy-managed-disk). If you need to revert any changes made while troubleshooting, you can use the snapshot to recreate the disk.

### Online repair by using Azure Serial Console

1. Connect to the VM using the Azure Serial Console, then [start a PowerShell session](https://docs.microsoft.com/troubleshoot/azure/virtual-machines/serial-console-windows#use-serial-console). If the Azure Serial Console doesn't work, connect to the VM using remote PowerShell. For more information, see [How to use remote tools to troubleshoot Azure VM issues](remote-tools-troubleshoot-azure-vm-issues.md).

1. After you connect to the VM, run the following command to list the user profiles entries. Locate any profiles that have the ".bak" extension on the end of the name.

    ```powershell
    reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList" /s | more
    ```

1. Remove all the user profile entries that end with ".bak", except for the built-in system accounts S-1-5-18, S-1-5-19 and S-1-5-20:

    ```powershell
    reg delete "HKLM\SOFTWARE\Microsoft\WindowsNT\CurrentVersion\ProfileList\<GUID>.bak"
    ```

1. Try to connect to the VM and see if the problem is resolved.
1. If the problem continues to occur, you can try removing all the user profile entries except the built-in system accounts **S-1-5-18**, **S-1-5-19** and **S-1-5-20**.

### Offline repair

If you're unable to access the VM using the Azure Serial Console or other remote tools, then the repair must be done in offline mode.

1. Follow the steps 1-3 of the [VM Repair process](https://docs.microsoft.com/troubleshoot/azure/virtual-machines/repair-windows-vm-using-azure-virtual-machine-repair-commands) to create a Repair VM. A copied OS disk of the failed VM will be attached to the Repair VM automatically. Usually the disk is attached as drive F.
1. Connect to the Repair VM.
1. On the Repair VM, start Registry Editor (regedit.exe). Select the **HKEY_LOCAL_MACHINE** key, select **File** > **Load Hive** from the menu. Locate and load the SOFTWARE hive file in the **F:\Windows\System32\config** folder, and then provide a name for the hive, example "RepairSOFTWARE".
1. Navigate to **RepairSOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList** and remove any registry keys that end in ".BAK". 
1. Use step 5 of the [VM Repair process](https://docs.microsoft.com/troubleshoot/azure/virtual-machines/repair-windows-vm-using-azure-virtual-machine-repair-commands) to mount the repaired OS disk to the failed VM.
1. Start the failed VM and try to connect to the VM using RDP. If the problem continues to occur, you can try removing all the user profile entries except the built-in system accounts **S-1-5-18**, **S-1-5-19** and **S-1-5-20**.

## **Recommended Documents**

* [Troubleshoot Remote Desktop connections to an Azure virtual machine](https://docs.microsoft.com/azure/virtual-machines/troubleshooting/troubleshoot-rdp-connection)
