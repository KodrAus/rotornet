//This is currently a scratchpad for the FSM API
//It needs to be safe, fast, easy to grok, and composable

open System

type Response<'m> =
| Ok
| Done
| Error of string
| Deadline of TimeSpan

//As far as IMachine knows, machines are immutable
//This means their state changes should have no obserable consequences
type IMachine<'c, 's> =
    abstract member Create :    'c -> 's -> Response<'m>
    abstract member Ready :     'c -> 's -> Response<'m>
    abstract member Wakeup :    'c -> 's -> Response<'m>
    abstract member Timeout :   'c -> 's -> Response<'m>

//A machine with a mutable internal state
type Counter<'c, 's> (s) =
    //Our mutable state
    let mutable state = s

    interface IMachine<'c, 's> with
        //When a machine is created internally for a socket
        //This is not something you call yourself
        //In this case, we construct the machine ourselves so don't need this
        member this.Create c s =
            printfn "Counter %i" state
            Ok

        //When a machine is ready to run
        //This corresponds to a socket ready call
        member this.Ready c s =
            printfn "Ready"
            Deadline(TimeSpan.FromSeconds(1.0))

        //When a machine receives a wakeup
        member this.Wakeup c s =
            match state with
            | x when x < 0 -> Error "state was less than 0" //We expect the counter to be greater than 0
            | 0 -> Done //If the counter is less than 0, we close this machine
            | x -> 
                state <- state - 1
                Deadline(TimeSpan.FromSeconds(1.0)) //If the counter is greater than 0, we continue

        //When a machine timeout occurs
        member this.Timeout c s =
            Done

//A machine with no state that does nothing
type Nothing<'c, 's> (state) =
    interface IMachine<'c, 's> with
        member this.Create c s =
            printfn "Nothing"
            Ok
        member this.Ready c s =
            Done
        member this.Wakeup c s =
            Done
        member this.Timeout c s =
            Done

//An anonymous machine
let machine s =
    let mutable state = Some(s)
    { 
        new IMachine<unit, unit> with
            member this.Create c s =
                printfn "Anonymous"
                Ok
            member this.Ready c s =
                Done
            member this.Wakeup c s =
                match state with
                | Some(s) -> 
                            printfn "%s" s
                            state <- None
                            Deadline(TimeSpan.FromSeconds(1.0))
                | None -> Done
            member this.Timeout c s =
                Done 
    }

[<EntryPoint>]
let main argv = 
    //Build some machines and iterate over them
    let machines = [ 
        Counter(3) :> IMachine<unit, unit>; 
        Nothing() :> IMachine<unit, unit>;
        machine "1";
        machine "2";
        //An inline machine
        { 
            new IMachine<unit, unit> with
                member this.Create c s =
                    printfn "Inline"
                    Ok
                member this.Ready c s =
                    Done
                member this.Wakeup c s =
                    Done
                member this.Timeout c s =
                    Done 
        };
    ]

    //Example of running some machines
    machines 
    |> List.iter(
        fun m -> 
            let created = m.Create () ()
            match created with
            | Ok -> 
                    let rec run (m: IMachine<_,_>) =
                        match (m.Wakeup () ()) with
                        | Error e -> printfn "Error: %s" e
                        | Done -> printfn "Done"
                        | _ -> printfn "Still Working..."
                               run m
                    run m
            | _ -> ()
       )

    0
