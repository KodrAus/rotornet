open System
open Rotor.Fsm

//A machine with a mutable internal state
type Counter<'c, 's> (s) =
    //Our mutable state
    let mutable state = s

    interface IMachine<'c, 's> with
        member this.create c s =
            printfn "Counter %i" state
            Ok

        member this.ready c s =
            printfn "Ready"
            Deadline(TimeSpan.FromSeconds(1.0))

        member this.wakeup c s =
            match state with
            | x when x < 0 ->   Error "state was less than 0"

            | 0 ->              Done

            | x ->              state <- state - 1
                                Deadline(TimeSpan.FromSeconds(1.0))

        //When a machine timeout occurs
        member this.timeout c s =
            Done

//A machine with no state that does nothing
type Nothing<'c, 's> (state) =
    interface IMachine<'c, 's> with
        member this.create c s =
            printfn "Nothing"
            Ok
        member this.ready c s =
            Done
        member this.wakeup c s =
            Done
        member this.timeout c s =
            Done

//An anonymous machine
let machine s =
    let mutable state = Some(s)
    { 
        new IMachine<unit, unit> with
            member this.create c s =
                printfn "Anonymous"
                Ok

            member this.ready c s =
                Done

            member this.wakeup c s =
                match state with
                | Some(s) ->    printfn "%s" s
                                state <- None
                                Deadline(TimeSpan.FromSeconds(1.0))

                | None ->       Done

            member this.timeout c s =
                Done 
    }

[<EntryPoint>]
let main argv = 
    let machines = 
        [ 
            Counter(3) :> IMachine<unit, unit>; 
            Nothing() :> IMachine<unit, unit>;
            machine "1";
            machine "2";
            //An inline machine
            { 
                new IMachine<unit, unit> with
                    member this.create c s =
                        printfn "Inline"
                        Ok
                    member this.ready c s =
                        Done
                    member this.wakeup c s =
                        Done
                    member this.timeout c s =
                        Done 
            };
        ]

    (build () () machines) |> run