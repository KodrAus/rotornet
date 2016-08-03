open System
open System.Threading
open Rotor.Fsm.Base

//A machine with a mutable internal state
type Machine<'c> (s) =
    inherit IMachine<'c>()

    //Our mutable state
    let mutable state = s

    override this.create c s =
        printfn "Created with counter %i" state
        Response.Deadline(5000UL)

    //On wakeup, set a new deadline.
    //Machines only have a single deadline, so if it's changed that's what's used
    override this.wakeup c s =
        printfn "Updating deadline"
        Response.Deadline(1000UL)

    //When a machine timeout occurs, check state and go back to sleep
    override this.timeout c s =
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
                                        new Machine<_>(10))
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
    notify (notifier.Value.wakeup())

    handle.Join()
    0