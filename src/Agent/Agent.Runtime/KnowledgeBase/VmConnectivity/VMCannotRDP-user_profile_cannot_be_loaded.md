# RDP failed because the user profile could not be loaded
<!--issueDescription-->
The RDP connection to virtual machine (VM) <!--$vmname-->[vmname]<!--/$vmname--> failed due to the following error message:

```
The User Profile Service failed the logon. User profile cannot be loaded.
```

or

```
The User Profile Service failed the sign-in. User profile cannot be loaded.
```

In this scenario it may only be users that had never logged on to the VM that are failing, and users that had previously logged on may still work.
<!--/issueDescription-->

## Recommended Steps

This error may occur if there is an issue with the default user profile preventing new user profile creation.

1. Use [Azure Serial Console](https://docs.microsoft.com/troubleshoot/azure/virtual-machines/serial-console-windows) to start a CMD prompt in the VM.
1. Backup the ProfileList registry key:
   
   ```
   reg export "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList" C:\Windows\System32\Config\ProfileList_Backup.reg
   ```

1. Check if any ProfileList subkeys end in ".bak":
   
   ```
   reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList"
   ```

1. If there are any ProfileList subkeys that end in ".bak", delete them, then retry the RDP connection:
   
   ```
   reg delete "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\<GUID>.BAK"
   ```

1. If the problem persists, review the following articles
   * [Error occurs during desktop setup and desktop location is unavailable when you log on to Windows for the first time](https://docs.microsoft.com/troubleshoot/windows-server/user-profiles-and-logon/desktop-location-unavailable)
   
   * [User profiles may fail to load after you install the Windows 8.1, or Windows Server 2012 R2 April 2014 update](https://docs.microsoft.com/troubleshoot/windows-server/user-profiles-and-logon/user-profiles-may-fail-load)
