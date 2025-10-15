# RDP failed due to protocol error 0x1104 detected at the client
<!--issueDescription-->
The RDP connection to virtual machine (VM) <!--$vmname-->[vmname]<!--/$vmname--> failed due to the following error message:

```
Remote Desktop Connection
Because of a protocol error detected at the client (code 0x1104), this session will be disconnected.
Please try connecting to the remote computer again.
```

<!--/issueDescription-->

## **Recommended Steps**

To resolve this issue, make sure the Remote Desktop Services service (TermService) is configured to listen on port 3389. Or, if you have an application configured to listen on 3389 that you do not want to change, then change TermService to listen to a port other than 3389, and specify the other port when making an RDP connection.

1. Use [Azure Serial Console](https://docs.microsoft.com/troubleshoot/azure/virtual-machines/serial-console-windows) to start a PowerShell session in the VM.
1. Run the following command to see which service is listening on port 3389:
   
   ```
   netstat -anob
   ```

1. If a service other than TermService is listening on 3389, you can stop that service, restart TermService, and then try making an RDP connection to the VM.
   
   ```
   Stop-Service <name of service other than TermService that is listening on 3389>
   Restart-Service TermService -Force
   ```

1.  If a service besides TermService is listening on 3389 but you prefer to change TermService to listen on a different port, follow the PowerShell steps in [Change the listening port for Remote Desktop on your computer](https://docs.microsoft.com/windows-server/remote/remote-desktop-services/clients/change-listening-port).

1.  To use a port other than 3389 when making an RDP connection, add a colon and the port number to the VM's IP address in the Computer field of the Remote Desktop Connection, or when running it with the MSTSC command:
    
    ```
    mstsc /v:<ipaddress>:<port>
    ```
