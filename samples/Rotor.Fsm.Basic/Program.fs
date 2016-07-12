open System
open System.Threading
open Rotor.Fsm

//A machine with a mutable internal state
type Counter<'c> (s) =
    //Our mutable state
    let mutable state = s

    interface IMachine<'c> with
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

            | x ->              printfn "Ping"
                                state <- state - 1
                                Deadline(TimeSpan.FromSeconds(1.0))

        //When a machine timeout occurs
        member this.timeout c s =
            Done

//A machine with no state that does nothing
type Nothing<'c> (state) =
    interface IMachine<'c> with
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
        new IMachine<unit> with
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
    let mutable notifier = None

    let handle = Async.StartAsTask <| async {
        let l = (loop ()) 
        l.addMachine (
            fun scope -> 
                notifier <- Some(scope.notifier())
                (Counter(3) :> IMachine<unit>)
        )
        l |> run
    }

    printfn "Running"

    while notifier.IsNone do
        ()

    //TODO: Swallow exception in waking up incomplete handle
    Thread.Sleep(5000)

    printfn "Waking up"

    notifier.Value.wakeup()

    printfn "Woken up"

    handle.Wait()
    0