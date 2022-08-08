#r "nuget: Akka.FSharp"

open Akka
open Akka.FSharp
open Akka.Actor
open System
open FSharp.Collections
open System.Security.Cryptography


// State of simulator actor
type SimulatorState = {
    NumOfNodes: int
    NumOfHops: int
    ZerothPeer: IActorRef
}




// Messages
type Message =
    // Simulator messages
    | Create of (int * int * IActorRef)
    | JoinNetwork 
    | InitializeFinger of ( int * IActorRef)
    | CreateNetwork
    | Join of (int * IActorRef * IActorRef )
    | Data of String
    // Peer messages
    | FindSuccessorFor of (int * IActorRef)
    | SetPredecessor of (int * IActorRef)
    | SetSuccessor of (int * IActorRef)
    | FixFingers
    | FindSuccessorForFix of int * int
    | FixIndex of (int * IActorRef) * int



// initialize actor system
let systemRef = ActorSystem.Create("System")
let mutable numNodes = 5
let mutable curNumNodes = 4
let mutable m = 0
 

// hash function
let generateHash(key: int) =
    let idByte = BitConverter.GetBytes(key)
    let hash = HashAlgorithm.Create("SHA1").ComputeHash(idByte)
    BitConverter.ToString(hash).Replace("-", "").ToLower()


// Peer Actor Definition
let peer (id: int) (mailbox:Actor<_>) =
    printfn "[Peer] Node%d spawned." id
    let mutable Successor: (int * IActorRef)=(-1, null);
    let mutable FingerTable: (int * IActorRef) []=[||];
    let mutable ID: int = id;
    let mutable SimulatorRef: IActorRef = null;
    let mutable Next: int = 1;
    
    let rec loop state = actor {
        
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        
        match message with
        | Create (id,sid,succ)->
           printf "%A creating " mailbox.Self.Path.Name
           ID <- id
           Successor <- (sid,succ)
           printfn "Successor %A" Successor
           SimulatorRef <- mailbox.Sender()
           (*for i in 1..(m-1) do
            Array.append FingerTable [|(id + (pown 2 (i|>int)) |> int,)|]*)
        | InitializeFinger(fid,fref) ->
            FingerTable <- Array.append FingerTable [|fid,fref|]
            printfn "Updating %A finger table to %A" id id


        | Join (key,ref,peerZero) ->
            printfn "%A Joining from %A" mailbox.Self.Path.Name peerZero
            peerZero <! FindSuccessorFor(key,ref)
            //printfn "Join after node %A" x
            
        | FindSuccessorFor (key,ref) ->
            //printfn "%A Finding successor of %A " mailbox.Self.Path.Name key
            if (key > ID) && (key <= (fst Successor)) then 
                //printfn "Value lie between me and my Successor%A" Successor
                //mailbox.Sender() <! SetSuccessor (fst Successor,snd Successor)
                System.Threading.Thread.Sleep(1000)
                mailbox.Self <! SetSuccessor (key,ref)
                return! loop()
                
            else
                //printfn "%A ID %A succ" ID Successor
                if (fst Successor) = 0 then 
                    //printfn "Its a last Node"
                    //mailbox.Sender() <! SetSuccessor (fst Successor,snd Successor)
                    System.Threading.Thread.Sleep(1000)
                    mailbox.Self <! SetSuccessor (key,ref)
                    return! loop()
                else 
                    //printfn "Check my finger table and hop"
                    for i = m downto 1 do 
                        let fv = fst(Array.get FingerTable (i-1))
                        //printfn "FT value: %A" fv
                        if fv > ID && fv <=key then 
                            snd(Array.get FingerTable (i-1)) <! FindSuccessorFor(key,ref)
            return! loop()
        | SetSuccessor (key,peer) ->
            printfn "Setting %A successor to %A called by %A" mailbox.Self.Path.Name peer mailbox.Sender
            Successor <- (key,peer)
            //printfn "ID:%A Successor:%A" ID Successor
            return! loop()
       
        | FixFingers ->
            printfn "[%s] Running FixFingers" mailbox.Self.Path.Name
            

        | FindSuccessorForFix (key, ind) ->
            printfn "[%s] Finding successor for fixing index %d" mailbox.Self.Path.Name ind
            

        | FixIndex (peer, ind) ->
            printfn "[%s] Updating finger table at index %d." mailbox.Self.Path.Name ind
        | _ ->  failwith "[ERROR] Unknown message."
        return! loop()
    }

    loop ()

let mutable peers = [||]
// Simulator Actor Definition
let simulator (numNodes: int) (mailbox:Actor<_>) =
   
    
    let rec loop state = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        

        match message with
        | CreateNetwork -> 
                let zeroThPeer = spawn systemRef "Node0" (peer(0))
                peers <- Array.append peers [|zeroThPeer|]
                let tenThPeer = spawn systemRef "Node1" (peer(1))
                peers <- Array.append peers [|tenThPeer|]
                let twentyThPeer = spawn systemRef "Node2" (peer(2))
                peers <- Array.append peers [|twentyThPeer|]
                let thirtyThPeer = spawn systemRef "Node3" (peer(3))
                peers <- Array.append peers [|thirtyThPeer|]

                m <- (Math.Log2(float curNumNodes) |> int)
                
                zeroThPeer <! Create(0,1,tenThPeer)
                System.Threading.Thread.Sleep(1000)
                tenThPeer <! Create(1,2,twentyThPeer)
                System.Threading.Thread.Sleep(1000)
                twentyThPeer <! Create(2,3,thirtyThPeer)
                System.Threading.Thread.Sleep(1000)
                thirtyThPeer <! Create(3,0,zeroThPeer)

                for j in 0 .. peers.Length-1 do
                    for i in 1..(m) do
                        let mutable  x = j + pown 2 (i-1)
                        if x > curNumNodes-1 then 
                            x <- 0
                        Array.get peers j<! InitializeFinger(x,Array.get peers x)
                        System.Threading.Thread.Sleep(1000)
                //printfn "Peer list before calling join is %A" peers
                mailbox.Self <! JoinNetwork
            | JoinNetwork ->
                //printfn "Join called by %A" mailbox.Self
                //printfn "Peer list after calling join is %A" peers
                if curNumNodes < numNodes then 
                    curNumNodes <- curNumNodes + 1
                    let node = spawn systemRef ("Node"+curNumNodes.ToString()) (peer(curNumNodes))
                    peers <- Array.append peers [|node|]
                    //printfn "Peer list is %A" peers
                    node <! Join(curNumNodes, node, Array.get peers 0)









        | _ ->  failwith "[ERROR] Unknown message."
        return! loop()
    }

    loop ()


// Start of the program
let simulat = spawn systemRef "simulator" (simulator(numNodes)) 
simulat <! CreateNetwork
//simulat <! JoinNetwork

systemRef.WhenTerminated.Wait()
    

// Define a function to construct a message to print
let from whom =
    sprintf "from %s" whom

let message = from "F#" // Call the function
printfn "Hello world %b" ("r2">"s1")