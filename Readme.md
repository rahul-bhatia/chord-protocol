# Project 3 : Peer to Peer Chord.

## Team Memebers 
- Rahul Bhatia (3427-1390)
- Simran Bhagwandasani (7197-3082)
## How to Run?
`dotnet fsi chordP2p.fsx Nodes Request` <br>
Here `Nodes` is no. of actors we need to spawn to participate in the chord and `Request` is the number of keys every node should lookup for.

## What is working?
- Given the number of node, algorithm spawns those many actors and set them into chord using `Join` function.
- After every successful operation `MakeFingerEntry` fixes the fingers of all the nodes on the basis of current number of nodes joined.
- After the Chord is created and `Stabilized` which is when all the nodes perform Join, We call Lookup operations for any random key with all the `Peers` as a point of entry.
- All the peers calls `Lookup` for `Request` number of times and calculate the average number of hops in every `Request`
- The Algorithm then computes the **Average Number of Hops**  for all given actors as when they are computing `Hops`.

## Observations

### Largest Network
- we were able to spawn 6000 chords for which `AverageHops` came out to be **9sh**


### Hops vs (Request,Nodes)

| Nodes |Requests |  Hops |
| --- | ----------- | ------- |
| 8 | 100| 1.35 |
| 32| 50 | 2.9 |
| 64| 20 | 3.3 |
| 256| 20 | 4.29 |
| 1024| 10 | 4.59 |



 