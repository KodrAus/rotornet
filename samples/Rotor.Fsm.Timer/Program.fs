open System
open System.Threading
open Rotor.Fsm

//A machine with a mutable internal state
type Counter<'c> (s) =
    //Our mutable state
    let mutable state = s

    interface IMachine<'c> with
        member this.create c s =
            printfn "Created with counter %i" state
            Response.Deadline(2000UL)

        member this.ready c s =
            Response.Ok

        member this.wakeup c s =
            Done

        //When a machine timeout occurs
        member this.timeout c s =
            match state with
            | x when x < 0 ->   Error "state was less than 0"

            | 0 ->              Done

            | x ->              printfn "Hello %i" x
                                state <- state - 1
                                Response.Deadline(500UL)

[<EntryPoint>]
let main argv = 
    //Spin up an io loop in a thread
    let l = (loop ())
    l.addMachine (fun scope -> (Counter(10) :> IMachine<unit>))
    l |> run
    0