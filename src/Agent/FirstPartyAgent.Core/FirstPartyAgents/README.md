# Why this folder?

The purpose of this folder is to organize all **Microsoft.App/Agents**-related Azure Resources and Agent configurations. Below are the key objectives:

- **Configuration Management**:  
  - Load specific configurations for a given Agent using the `firstPartyConfiguration` JSON config object or privileged environment variables.  
  - Future support for dynamically overriding the MetaAgent system prompt (currently static, with logic implemented in this class).  

- **Dependency Injection**:  
  - Enable 'magical' dependency injection functionality for First Party sub-agents.  
  - Simplify sub-agent creation by allowing developers to focus only on defining their sub-agent and related service implementation.  
  - Automatically handle configuration settings, service implementations, plugins, and other dependencies within this folder.
    Eventually this will let Meta Agent discover them and adjust it's capabilities to coordinate with sub-agents automatically.


## FAQs

### I have created my First party sub-agent, what specific changes I need in this folder to make it work ?
As we are working on 'magical' dependency injection, you need to follow 'HelloWorldAgent' as a reference and your configuration, service implementation, and other dependencies need to specified.
Once we refactor this code, you don't need to make ANY change to this folder as long as your sub-agent following defined pattern (that's the end goal !)
