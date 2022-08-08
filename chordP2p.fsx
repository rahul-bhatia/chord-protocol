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
    | Create of (int * IActorRef)
    | JoinNetwork 
    | CallLookups
    | MakeFingers
    | MakeFingerEntry of (int * IActorRef)
    | Lookup of (int * int * IActorRef)
    | InitializeFinger of ( int * IActorRef)
    | CreateNetwork
    | GotKeyPosition of (IActorRef * int * int * IActorRef)
    | Join of (int * IActorRef * int * IActorRef)
    | Data of String
    | LookupMaster of ( int * int)
    | FindSuccessorFor of (int * IActorRef)
    | SetPredecessor of (int * IActorRef)
    | SetSuccessor of (int * IActorRef)
    | FixFingers
    | Passed
    | FindSuccessorForFix of int * int
    | FixIndex of (int * IActorRef) * int



// initialize actor system
let systemRef = ActorSystem.Create("System")
let mutable numNodes = fsi.CommandLineArgs.[1] |> int
let mutable m = 0
 

// hash function
let generateHash(key: int) =
    let idByte = BitConverter.GetBytes(key)
    let hash = HashAlgorithm.Create("SHA1").ComputeHash(idByte)
    BitConverter.ToString(hash).Replace("-", "").ToLower()

let mutable peers = [||]
let mutable flag = true


// Peer Actor Definition
let peer (id: int) (mailbox:Actor<_>) =
    
    let mutable Successor: (int * IActorRef)=(-1, null);
    let mutable Predecessor: (int * IActorRef)=(-1, null);
    let mutable FingerTable: (int * IActorRef) []=[||];
    let mutable ID: int = id;
    let mutable SimulatorRef: IActorRef = null;
    let mutable Next: IActorRef = null;
    
    let rec loop state = actor {
        
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        
        match message with
        | Create (id,master)-> 
            //printfn "[Peer] Node%d spawned." id
            ID <- id
            SimulatorRef <- master
        | Join (sucid,suc,preid,pre)->
            Successor <- (sucid,suc)
            Predecessor <- (preid,pre)
            //printfn "State is :%A<-%A->%A" (fst Predecessor) ID (fst Successor)
        | MakeFingerEntry(id,ref) ->
           // printfn "Updating %A's finger table" id
            for i in 0..m-1 do 
                //printfn "Find id of id%A + %A=>%A" id (pown 2 i) (id+(pown 2 i))
                let mutable ft = 0
                for j in 0..peers.Length-2 do 
                    let ele = (fst (Array.get peers j))
                    let scnd = (fst (Array.get peers (j+1)))
                    if ((id+(pown 2 i)) > ele) && ((id+(pown 2 i)) <= scnd) then
                        //printfn "%A points to %A" (id+(pown 2 i)) snd 
                        FingerTable <- Array.append FingerTable [|(scnd , snd (Array.get peers (j+1)))|]
                        ft <- 1
                if ft = 0  then
                    //printfn "%A points to %A" (id+(pown 2 i)) 0 
                    FingerTable <- Array.append FingerTable [|(fst (Array.get peers (0)), snd (Array.get peers (0)))|]
            //printfn "%A's finger table:%A" id FingerTable
        | Lookup(key,hops,caller) ->
            let mutable ftfound = 0
            let mutable hps = hops
           //printfn "%A looking for key:%A" ID key
            if key = ID && flag then 
                flag <- false
                SimulatorRef <! GotKeyPosition((snd Successor),key,hps,caller)
                mailbox.Self <! Passed
                
            
            elif (key > ID) && (key <= (fst Successor)) then
                //printfn "Key lies between %A and %A" ID Successor
                //hps <- hps+1 
                flag <- false
                SimulatorRef <! GotKeyPosition((snd Successor),key,hps,caller)
                mailbox.Self <! Passed
                
            else
                if ID > (fst Successor) && flag then  //last node  
                    hps <- hps+1 
                    flag <- false
                    SimulatorRef <! GotKeyPosition((snd (Array.get peers (0))),key,hps,caller)
                    mailbox.Self <! Passed
                else 
                     
                    //printf "Check finger table"
                    let mutable i = m-1
                    while i > 0 && flag do
                    //for i= m-1 downto 0 do
                        let fv = (fst (Array.get FingerTable i))
                        //printfn "checking fv:%A of %A" fv ID
                        if fv < ID && flag then //fv of first node 
                            //printfn "abort 1"
                            flag <- false
                            //System.Threading.Thread.Sleep(500)
                            hps <- hps+1
                            SimulatorRef <! GotKeyPosition((snd (Array.get peers (0))),key,hps,caller)
                        elif (fv > ID && fv <= key)  && flag then
                            ftfound <- 1
                            //printfn "call this ref%A" fv
                            //flag <- false
                            //System.Threading.Thread.Sleep(500)
                            hps <- hps+1
                            if hps>m then
                                hps<-hps/2
                            (snd (Array.get FingerTable i)) <! Lookup(key,hps,caller)
                        i <- 0
                        System.Threading.Thread.Sleep(500)
                    if ftfound = 0 && flag then //when poe.id > key
                        //hps <- hps+1 
                        //flag <- false
                        hps <- hps+1
                        if hps>m then
                                hps<-hps/2
                        (snd (Array.get FingerTable (m-1))) <! Lookup(key,hps,caller)
            mailbox.Self <! Passed
        |  FixIndex (peer, ind) ->
            printfn "[%s] Updating finger table at index %d." mailbox.Self.Path.Name ind
            let mutable tempFingerTable = Array.get FingerTable ind
            //Array.set tempFingerTable  Array.get FingerTable ind
            printfn "[%s] New finger table is %A" mailbox.Self.Path.Name tempFingerTable
            //FingerTable <- tempFingerTable.[0]
            return! loop() 
        | Passed -> printf "" |> ignore
        | _ ->  failwith "[ERROR] Unknown message." |> ignore
        return! loop()
    }

    loop ()

let mutable sum =0
let mutable avg = 0.0

// Simulator Actor Definition
let simulator (numNodes: int) (mailbox:Actor<_>) =
 
    let rec loop state = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        let mutable maxId=0
        let r = System.Random()

        match message with
        | CreateNetwork ->
            m <- (Math.Log2(float numNodes) |> int)
            for i in 1..numNodes do
                let id = r.Next(maxId+1,(maxId+(Math.Log (numNodes |> float,2.0)  |> int)))
                //let id = maxId+(numNodes/2)
                maxId <- id
                let node  = spawn systemRef ("Node"+(generateHash id)) (peer(maxId))
                peers <- Array.append peers [|(maxId,node)|]
                node <! Create (maxId,mailbox.Self)
                //System.Threading.Thread.Sleep(500)
            printfn "[Master]-Spawned %A actors" numNodes
            mailbox.Self <! JoinNetwork
        | JoinNetwork ->
            //printfn "%A" peers
            for i in 0..numNodes-1 do 
                let node1 = Array.get peers i
                let mutable suci = i+1
                let mutable prei = i-1
                if i = 0 then 
                    prei <- (numNodes - 1)
                if i = (numNodes - 1) then 
                    suci <- 0
                let pre = Array.get peers (prei)
                let suc = Array.get peers (suci)
                snd node1 <! Join (fst suc,snd suc ,fst pre,snd pre)
                System.Threading.Thread.Sleep(200)
                
                //printfn "[Peer]%A Fixing fingers" (snd (Array.get peers i)).Path.Name
            mailbox.Self <! MakeFingers
        | MakeFingers ->
            //printfn "%A peer is " (snd (Array.get peers 0))
            for i in 0..numNodes-1 do 
                let x = Array.get peers (i)
                snd x <! MakeFingerEntry(fst x,snd x)
                System.Threading.Thread.Sleep(200)
            //(snd (Array.get peers 0)) <!  Lookup (20,0)
            //System.Threading.Thread.Sleep(2000)
            //flag <- true
            //(snd (Array.get peers 0)) <!  Lookup (10,0)
            mailbox.Self <! CallLookups 
        | CallLookups ->
            //printfn "Peers : %A" peers
            let mutable key = 0
            for i in 0..numNodes-1 do
                key <- key + (fst (Array.get peers i))
            key <- key / peers.Length
            key <- key |> int
            for i in 0..numNodes-1 do
                //printfn "--"
                let mutable sum = 0
                for j in 1..(fsi.CommandLineArgs.[2] |> int) do
                    flag <- true
                    (snd (Array.get peers i)) <!  Lookup (r.Next(key),0, (snd (Array.get peers i)))
                    System.Threading.Thread.Sleep(500)
                //System.Threading.Thread.Sleep(2000)
        | LookupMaster (key,id) -> 
            //printfn "Failing here?"
            (snd (Array.get peers id)) <!  Lookup (key,0, (snd (Array.get peers id)))
        | GotKeyPosition (ref, key,hops,caller) ->
            sum <- sum + (hops )
            avg <- (sum |> float) / (numNodes  * (fsi.CommandLineArgs.[2] |> int) |> float)  
            printfn "Input %A:Set key%A on node %A with hops = %A ,Avg:%A" (caller.Path.Name.ToString()) (key) (ref.Path.Name.ToString()) (hops) avg
        | _ ->  failwith "[ERROR] Unknown message."

        
        return! loop()
    }

    loop ()


// Start of the program
let simulat = spawn systemRef "simulator" (simulator(numNodes)) 
simulat <! CreateNetwork
//System.Threading.Thread.Sleep(10000)
//System.Threading.Thread.Sleep(10000)

systemRef.WhenTerminated.Wait()
    

// Define a function to construct a message to print
let from whom =
    sprintf "from %s" whom

let message = from "F#" // Call the function
printfn "Hello world %b" ("r2">"s1")