/// # `Rotor.Fsm`
///
/// Base state machine implementation for asynchronous io.

//State: First Principals implementation

namespace Rotor.Fsm

module Base =
    open System
    open System.Threading
    open System.Collections.Concurrent
    open Rotor.Libuv.Networking

    /// The kind of response returned by an invokation of a state machine.
    type Response =
    | Ok
    | Done
    | Error of string
    | Deadline of uint64

    /// A way for objects outside of the loop to wakeup a state machine.
    /// 
    /// The `Notifier` is specific to a single machine.
    /// This is the external part of the notification channel.
    type Notifier(token: int64, handle: UvAsyncHandle, queue: ConcurrentQueue<int64>) =
        member this.wakeup () =
            queue.Enqueue(token)
            try handle.Send()
                NotifyResponse.Ok
            with 
            | :? NullReferenceException -> NotifyResponse.Retry(RetryNotifier(handle))
            | :? ObjectDisposedException -> NotifyResponse.Closed

    /// A way to retry sending a ping to a machine.
    /// 
    /// This does not queue the message subsequent times.
    and [<Struct>] RetryNotifier(handle: UvAsyncHandle) =
        member this.wakeup () =
            try handle.Send()
                NotifyResponse.Ok
            with
            | :? NullReferenceException -> NotifyResponse.Retry(RetryNotifier(handle))
            | :? ObjectDisposedException -> NotifyResponse.Closed

    /// The response from calling `wakeup` on a `Notifier`.
    /// 
    /// It can either indicate a successful result, or a failure with a `RetryNotifier`.
    and NotifyResponse =
        | Ok
        | Retry of RetryNotifier
        | Closed

    /// A wakeup handle containing a message queue and `UvAsyncHandle` for waking up
    /// the loop without blocking.
    /// 
    /// This is the internal part of the notification channel.
    /// When the `WakeupHandle` is disposed, all other Notifiers are invalidated.
    type WakeupHandle(handle: UvAsyncHandle, queue: ConcurrentQueue<int64>) =
        member this.init l f =      handle.Init(l, System.Action(f), null) |> ignore
        member this.notifier t =    Notifier(t, handle, queue)
        member this.dequeue () =    queue.TryDequeue()

        interface IDisposable with
            member this.Dispose () = 
                handle.Reference()
                handle.Dispose()

    type Scope(token: int64, wakeup: WakeupHandle) =
        member this.notifier() = wakeup.notifier token

    /// The base definition of a state machine.
    /// 
    /// State machines are generic over the context (`'c`) and scope which are inhected by the loop.
    [<AbstractClass>]
    type IMachine<'c>() =
        /// Called when a machine is created by the loop.
        /// 
        /// This is not something you'll call yourself, prefer using a constructor when pre-building machines.
        abstract member create :    'c -> Scope -> Response
        /// Called when a notification has been sent to this machine.
        abstract member wakeup :    'c -> Scope -> Response
        /// Called when a timer has expired.
        abstract member timeout :   'c -> Scope -> Response

        abstract member dispose: unit -> unit
        default this.dispose () = ()

        interface IDisposable with
            member this.Dispose() =
                this.dispose ()

    type private Machine<'c>(m: IMachine<'c>, t: UvTimerHandle) =
        member this.init l f =      t.Init(l, System.Action(f), null) |> ignore
        member this.machine =       m
        member this.timeout ms =    t.Start(ms, 0UL)

        interface IDisposable with
            member this.Dispose() =
                (m :> IDisposable).Dispose()
                t.Dispose()

    type private LoopState =
    | Idle of UvLoopHandle * WakeupHandle
    | Running of UvLoopHandle * WakeupHandle
    | Closed

    /// The main IO loop made up of state machines.
    /// 
    /// The loop manages the lifetimes of the machines and bound io and gives you a means to communicate
    /// from outside the loop itself.
    /// 
    /// When `run` is called, the loop will block the calling thread until complete.
    type Loop<'c>(ctx) =
        let libuv =                     Binding()
        let mutable loop =              Idle(
                                            new UvLoopHandle(), 
                                            new WakeupHandle(
                                                new UvAsyncHandle(), 
                                                new ConcurrentQueue<int64>()))
        let mutable context =           ctx
        let mutable machines: 
            Map<int64, Machine<'c>> =   Map.empty

        let stop () =
            match loop with
            | Idle(_, _) ->         ()

            | Closed ->             ()

            | Running(l, wakeup) -> (wakeup :> IDisposable).Dispose()
                                    l.Stop()
                                    (l :> IDisposable).Dispose()
                                    
                                    loop <- LoopState.Closed

        let remove token = 
            let machine = Map.find token machines
            (machine :> IDisposable).Dispose()

            machines <- Map.remove token machines

            match machines.Count with
            | 0 ->  stop()
            | _ ->  ()

        let runOnce token (fsm: Machine<'c>) f =
            match loop with
            | Closed -> ()
            | Idle(_, wakeup) | Running(_, wakeup) ->
                let scope = Scope(token, wakeup)
                let res = try f context scope with | e -> Error(e.Message)

                match res with
                | Done ->           remove token

                | Error(e) ->       printfn "Error running %i: '%s'" token e
                                    remove token

                | Deadline(t) ->    fsm.timeout t

                | Response.Ok ->    ()

        /// Add a generic `IMachine<'c>` to the loop.
        /// 
        /// This can only be done if the loop is in the `Idle` state.
        member this.addMachine (f: Scope -> 'a) =
            match loop with
            | Closed ->             raise (NotImplementedException("Adding machines to dirty loops is not implemented"))

            | Running(_, _) ->      ()

            | Idle(_, wakeup) ->    let token = int64 machines.Count
                                    let scope = Scope(token, wakeup)
                                    let machine = f scope

                                    machines <- Map.add token (new Machine<'c>((machine :> IMachine<'c>), new UvTimerHandle())) machines

        /// Run the loop.
        /// 
        /// This can only be done if the loop is in the `Idle` state and will block the calling thread.
        /// 
        /// Calling `run` will execute the `create` method of each machine, binding any io resources.
        /// It will also set up the `wakeup` infrastructure and any `Notifier`s returned previously
        /// will become available for use.
        /// 
        /// The `run` method won't terminate until all machines reach a state of `Done` or `Error`.
        member this.run () =
            match loop with
            | Closed ->         raise (NotImplementedException("Restarting dirty loops is not implemented"))

            | Running(_, _) ->  0

            | Idle(l, w) ->     loop <- LoopState.Running(l, w)
                                l.Init(libuv)
                                
                                //Initialise machines and timers
                                machines |> Map.iter(fun token fsm -> 
                                    fsm.init l (fun () -> runOnce token fsm fsm.machine.timeout)

                                    runOnce token fsm fsm.machine.create)

                                //Initialise the wakeup queue
                                w.init l (fun () -> 
                                            let rec handle () =
                                                match w.dequeue() with
                                                | true, token ->    match (Map.tryFind token machines) with
                                                                    | Some(fsm) -> runOnce token fsm fsm.machine.wakeup
                                                                    | _ -> ()

                                                                    handle()

                                                | false, _ ->       ()

                                            handle())

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
