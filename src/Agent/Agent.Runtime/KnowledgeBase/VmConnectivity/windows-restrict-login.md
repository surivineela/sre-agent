# The system administrator has restricted the times during which you may log in when you connect to an Azure VM
<!--issueDescription-->
We have determined that your Windows Virtual Machine (VM) <!--$vmname-->[vmname]<!--/$vmname--> is in an inaccessible state because the user logon times are restricted through domain policies. 

When you try to connect through RDP, you are getting the following error message: 
"The system administrator has restricted the times during which you may log in. Try logging in later. If the problem continues, contact your system administrator or technical support."

On the event logs you will also find the event 139 on Microsoft-Windows-RemoteDesktopServices-RdpCoreTS4Operational.evtx with error code -1073741713, which shows that "The user account has time restrictions and may not be logged onto at this time." 
<!--/issueDescription-->

## **Recommended Steps**

* The administrator of the VM has restricted your logon times. To resolve this problem, work with your administrator to determine what the logon time restrictions are and modify them if necessary. 

## **Recommended Documents**

* [Troubleshoot Remote Desktop connections to an Azure virtual machine](https://docs.microsoft.com/azure/virtual-machines/troubleshooting/troubleshoot-rdp-connection)
