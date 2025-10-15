# Authentication errors when connecting via RDP to a Windows Virtual Machine (VM) in Azure
<!--issueDescription-->
We have determined that your Windows Virtual Machine (VM) <!--$vmname-->[vmname]<!--/$vmname--> is in an inaccessible state due to authentication errors.
<!--/issueDescription-->

In this scenario, you receive one of the following error messages:

* An authentication error has occurred. The Local Security Authority cannot be contacted.
* The remote computer that you are trying to connect to requires Network Level Authentication (NLA), but your Windows domain controller cannot be contacted to perform NLA. If you are an administrator on the remote computer, you can disable NLA by using the options on the Remote tab of the System Properties dialog box.
* This computer can't connect to the remote computer. Try connecting again, if the problem continues, contact the owner of the remote computer or your network administrator.

## **Recommended Steps**

* How to [Troubleshoot authentication errors when you use RDP to connect to Azure VM](https://docs.microsoft.com/troubleshoot/azure/virtual-machines/cannot-connect-rdp-azure-vm).

## **Recommended Documents**

* [Troubleshoot Remote Desktop connections to an Azure virtual machine](https://docs.microsoft.com/azure/virtual-machines/troubleshooting/troubleshoot-rdp-connection)
