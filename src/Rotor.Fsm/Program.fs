/// # `Rotor.Fsm`
///
/// Base state machine implementation for asynchronous io.

//State: First Principals implementation

//TODO: Use KestrelThread as a base for building up this layer

namespace Rotor

module Fsm =
    open System
    open System.Collections.Concurrent
    open Rotor.Libuv.Networking

    /// The kind of response returned by an invokation of a state machine.
    type Response =
    | Ok
    | Done
    | Error of string
    | Deadline of TimeSpan

    type Notifier(token: int64, queue: BlockingCollection<int64>, wakeup: UvAsyncHandle) =
        member this.wakeup () =
            if queue.TryAdd(token) then 
                try wakeup.Send()
                    NotifyResponse.Ok
                with
                | NullReferenceException -> NotifyResponse.Retry(RetryNotifier(wakeup))
            else raise (NotImplementedException("Failed TryAdd not implemented"))
    and RetryNotifier(wakeup: UvAsyncHandle) =
        member this.wakeup () =
            try wakeup.Send()
                NotifyResponse.Ok
            with
            | NullReferenceException -> NotifyResponse.Retry(RetryNotifier(wakeup))
    and NotifyResponse =
        | Ok
        | Retry of RetryNotifier

    type Scope(token: int64, queue: BlockingCollection<int64>, wakeup: UvAsyncHandle) =
        member this.notifier() = Notifier(token, queue, wakeup)

    /// The base definition of a state machine.
    /// 
    /// State machines are generic over the context (`'c`) and scope which are inhected by the loop.
    type IMachine<'c> =
        /// Called when a machine is created by the loop.
        /// 
        /// This is not something you'll call yourself, prefer using a constructor when pre-building machines.
        abstract member create :    'c -> Scope -> Response
        /// Called when the socket associated with this machine is ready to operate on.
        abstract member ready :     'c -> Scope -> Response
        /// Called when a notification has been sent to this machine.
        abstract member wakeup :    'c -> Scope -> Response
        /// Called when a timer has expired.
        abstract member timeout :   'c -> Scope -> Response

    //TODO: Wrap UvLoopHandle in here to force checking
    type LoopState =
    | Idle of UvLoopHandle
    | Running of UvLoopHandle

    type Loop<'c>(ctx) =
        let libuv = Binding()
        let mutable loop = Idle(new UvLoopHandle())

        //TODO: Capture this in struct
        let wakeup = new UvAsyncHandle()
        let wakeupQueue = new BlockingCollection<int64>()

        /// A shared context that all machines receive. It's safely mutable because each machine receives it one at a time.
        let mutable context: 'c = ctx
        /// A list of machines that are bound to socket events.
        let mutable machines: Map<int64, IMachine<'c>> = Map.empty

        let stop =
            match loop with
            | Idle(_) ->    ()

            | Running(l) -> wakeup.Dispose()
                            l.Stop()
                            loop <- LoopState.Idle(l)
                            printfn "Stopped"

        /// Remove a machine from the list and check for any active machines
        let remove token = 
            machines <- Map.remove token machines

            match machines.Count with
            | 0 ->  printfn "Stopping loop"
                    stop
            | _ ->  printfn "Life goes on"

        let runOnce token f =
            let scope = Scope(token, wakeupQueue, wakeup)
            let res = try f context scope with | e -> Error(e.Message)

            match res with
            | Done ->           printfn "Done"
                                remove token

            | Error(e) ->       printfn "Error running %i: '%s'" token e
                                remove token

            | Deadline(t) ->    raise (NotImplementedException("Deadlines are not implemented"))

            | Response.Ok ->    ()

        member this.addMachine (f: Scope -> IMachine<'c>) =
            printfn "Adding machine"

            let token = int64 machines.Count
            let scope = Scope(token, wakeupQueue, wakeup)
            let machine = f scope

            machines <- Map.add token machine machines

        member this.run () =
            match loop with
            | Running(_) -> printfn "Loop is already running"
                            0

            | Idle(l) ->    l.Init(libuv)
                            loop <- LoopState.Running(l)
                            
                            //Create base machines
                            machines 
                            |> Map.iter(fun token machine -> runOnce token machine.create)

                            //Initialise the notifier
                            wakeup.Init(
                                l, 
                                System.Action(
                                    fun () -> 
                                        wakeupQueue.GetConsumingEnumerable()
                                        |> Seq.cast<int64>
                                        |> Seq.iter(
                                            fun token ->
                                                printfn "Wakeup setup"
                                                match (Map.tryFind token machines) with
                                                | Some(machine) -> runOnce token machine.wakeup
                                                | _ -> ()
                                        )
                                ), 
                                System.Action<_, _>(fun p1 p2 -> ())
                            ) |> ignore

                            match machines.Count with
                            | 0 -> 0
                            | _ -> l.Run()

    /// Build a loop with the given arguments.
    let loop c = Loop(c)

    /// Run a constructed loop.
    /// 
    /// This will block the calling thread until either the loop is stopped or all machines return a
    /// state of either `Done` or `Error`.
    let run (l: Loop<'c>) = l.run()

    let machine f (l: Loop<'c>) = 
        l.addMachine(f)
        l