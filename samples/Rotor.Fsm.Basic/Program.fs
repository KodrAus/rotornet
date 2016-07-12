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
            Response.Ok

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

[<EntryPoint>]
let main argv = 
    let mutable notifier = None

    //Spin up an io loop in a TPL thread
    let handle = Async.StartAsTask <| async {
        let l = (loop ())
        l.addMachine (
            fun scope -> 
                notifier <- Some(scope.notifier())
                (Counter(3) :> IMachine<unit>)
        )
        l |> run
    }

    printfn "Starting"

    //Dodgy spin  to wait until the notifier has a value
    while notifier.IsNone do ()

    printfn "Waking up"

    //Keep trying to call the wakeup handle until it succeeds 
    //If the loop was just built then the handle will fail
    let rec notify resp =
        match resp with
        | NotifyResponse.Ok ->  ()

        | Retry(retry) ->       Thread.Sleep(500)
                                printfn "Notification failed, retrying"

                                notify (retry.wakeup())
            
    notify (notifier.Value.wakeup())

    printfn "Woken up"

    handle.Wait()
    0