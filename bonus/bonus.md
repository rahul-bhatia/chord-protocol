# Project 3B: Nodes Deletion.

## What is working?
- All the APIs mentioned in the paper are working fine for a closed network created by master.
- As soon as we ask master to bring peer down, `Stabilize` functions is updating finger tables of previous peer.
- We still need to **FIGURE OUT** how to inform the nodes in the system, to run the Stabilize function with the reference of deleted entries.
