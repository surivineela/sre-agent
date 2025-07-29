# Overview

- For general stuff, see `Agent.Web/Client/README.md`

## Data Plane vs Control Plane

Data Plane == Agent Site

Control Plane == ARM

ARM will not work for cross-tenant (or at least a CORP account accessing an AME resource). Currently, certain features and functionalities depend on Control Plane (read: ARM) calls, such as incident management, settings, etc. Our goal going forward is to learn more heavily on Data Plane because of this.
