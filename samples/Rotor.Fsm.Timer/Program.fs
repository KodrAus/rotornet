open System
open Rotor.Fsm

//A machine with a mutable internal state
type Machine<'c> (s) =
    //Our mutable state
    let mutable state = s

    interface IMachine<'c> with
        member this.create c s =
            printfn "Created with counter %i" state
            Response.Deadline(100UL)

        member this.wakeup c s =
            Response.Done

        //When a machine timeout occurs, check our state and go back to sleep
        member this.timeout c s =
            match state with
            | x when x < 0 ->   Error "state was less than 0"

            | 0 ->              Done

            | x ->              printfn "Hello %i" x
                                state <- state - 1
                                Response.Deadline(100UL)

[<EntryPoint>]
let main argv = 
    let l = (loop ())
    l.addMachine (fun scope -> Machine(3))
    l |> run