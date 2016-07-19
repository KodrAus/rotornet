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
            Response.Deadline(5000UL)

        member this.ready c s =
            Response.Ok

        member this.wakeup c s =
            printfn "Updating deadline"
            Response.Deadline(1000UL)

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
    let mutable notifier = None

    //Spin up an io loop in a thread
    let handle = new Thread(fun (o: Object) ->
                                let l = (loop ())
                                l.addMachine (
                                    fun scope -> 
                                        notifier <- Some(scope.notifier())
                                        (Counter(10) :> IMachine<unit>)
                                )
                                l |> run)

    handle.Start()

    //Dodgy spin to wait until the notifier has a value
    while notifier.IsNone do ()

    //Keep trying to call the wakeup handle until it succeeds 
    //If the loop was just built then the handle will fail
    let rec notify resp =
        match resp with
        | NotifyResponse.Ok ->  ()

        | Retry(retry) ->       Thread.Sleep(500)
                                notify (retry.wakeup())
            
    //Send a few notifications to the loop
    notify (notifier.Value.wakeup())

    handle.Join()
    0