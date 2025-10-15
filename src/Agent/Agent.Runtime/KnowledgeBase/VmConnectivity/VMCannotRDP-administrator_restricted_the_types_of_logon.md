# RDP failed due to logon type restrictions
<!--issueDescription-->
The RDP connection to virtual machine (VM) <!--$vmname-->[vmname]<!--/$vmname--> failed due to the following error message:

```
Remote Desktop Connection
The system administrator has restricted the types of logon (network or interactive) that you may use. For assistance, contact your system administrator or technical support.
```

<!--/issueDescription-->

## Recommended Steps

This error may occur if the user account you are attempting to use for the RDP connection does not have **Allow log on through Remote Desktop Services**, or has **Deny access to this computer from the network** or **Deny log on through Remote Desktop Services**.

1. Use [Azure Serial Console](https://docs.microsoft.com/troubleshoot/azure/virtual-machines/serial-console-windows) to start a CMD prompt in the VM.
1. Backup the existing user rights assignments to a file:
   
   ```
   secedit /export /areas USER_RIGHTS /cfg C:\Windows\Temp\UserRightsBefore.txt
   ```

1. Apply the default standard security template:
   
   ```
   secedit /configure /cfg C:\Windows\INF\defltbase.inf /db defltbase.sdb /verbose
   ```

1. Refresh policies:
   
   ```
   gpupdate /force
   ```

1. Try making the RDP connection to the VM again. If you still receive the same error, restart the VM, then try the RDP connection again.
