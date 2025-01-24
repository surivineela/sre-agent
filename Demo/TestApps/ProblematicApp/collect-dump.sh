#!/bin/bash
# We assume that the target container bundled the dotnet-dump tool with attach enabled. And the process ID of the target dotnet app is 1.
# Besides, there's a dotnet_dump_cmd.txt file in the container that contains the dump command.
# The dotnet-dump is an internal build with the attach feature enabled.
./dotnet-dump attach -p 1 < dotnet_dump_cmd.txt