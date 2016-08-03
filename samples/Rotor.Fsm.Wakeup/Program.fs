open System
open System.Threading
open Rotor.Fsm

//A machine with a mutable internal state
type Machine<'c> (s) =
    inherit IMachine<'c>()

    //Our mutable state
    let mutable state = s

    override this.create c s =
        printfn "Created with counter %i" state
        Response.Ok

    //On wakeup, check state and go back to idling
    override this.wakeup c s =
        match state with
        | x when x < 0 ->   Error "state was less than 0"

        | 0 ->              Done

        | x ->              printfn "Hello %i" x
                            state <- state - 1
                            Response.Ok

    override this.timeout c s =
        Response.Done

[<EntryPoint>]
let main argv = 
    let mutable notifier = None

    //Spin up an io loop in a thread
    let handle = new Thread(fun (o: Object) ->
                                let l = (loop ())
                                l.addMachine (
                                    fun scope -> 
                                        notifier <- Some(scope.notifier())
                                        new Machine<_>(3))
                                l |> run |> ignore)

    handle.Start()

    //Dodgy spin to wait until the notifier has a value
    while notifier.IsNone do ()

    //Keep trying to call the wakeup handle until it succeeds 
    //If the loop was just built then the handle will fail
    let rec notify resp =
        match resp with
        | Retry(retry) ->       Thread.Sleep(500)
                                notify (retry.wakeup())
                                
        | _ ->                  ()
            
    //Send a few notifications to the loop
    //These notifications will continue to fire for a while after the loop stops.
    //In this case, the notifier returns a response of 'Closed' instead of 'Retry'.
    for i in 0 .. 30 do
        notify (notifier.Value.wakeup())
        Thread.Sleep(50)

    handle.Join()
    0