---
mode: 'agent'
---

I'm writing a postgresql component for a Azure SRE agent system. 
One step is that it crawls through the resources that the user provided and determines the topology of the infrastructure. 
Part of this is for AKS container environments, app service settings, etc. It looks for connection strings. 
I'm building the component that will be able to parse PostgreSQL connection strings and use that to find and 
connect databases to be added to the knowledge graph.

PostgreSQL connection parameters in the environment can be specified in a lot of different ways, 
and it depends on library, language, etc. You can have a single environment variable have the connection string, 
but also can have e.g. PGHOST, PGUSER etc specify the connection parameters.

I need to enumerate all of the different ways that a postgresql connection string can occur in an app's 
environment/settings so that the agents who are coding the parsing logic can make sure to handle all cases.

Please provide documentation on the complete breadth of ways that connection strings can appear in these settings, 
which I will use to inform the coding agents to cover all cases.

Also, consider what parameters in a connection string could be useful to encode in the knowledge graph. 
E.g. if the connection requires SSL, that is an interesting property that should be encoded on the edge 
connecting the app service to the database. Write up documentation about these properties and how they appear 
in the various forms of connection strings.