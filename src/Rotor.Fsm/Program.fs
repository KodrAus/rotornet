/// # `Rotor.Fsm`
///
/// Base state machine implementation for asynchronous io.

//State: First Principals implementation

//TODO: Use KestrelThread as a base for building up this layer

namespace Rotor

module Fsm =
    open System
    open System.Threading
    open System.Collections.Concurrent
    open Rotor.Libuv.Networking

    /// The kind of response returned by an invokation of a state machine.
    type Response =
    | Ok
    | Done
    | Error of string
    | Deadline of TimeSpan

    /// A wakeup handle containing a message queue and `UvAsyncHandle` for waking up
    /// the loop without blocking.
    type WakeupHandle(handle: UvAsyncHandle, queue: ConcurrentQueue<int64>) =
        member this.handle = handle
        member this.queue = queue

        interface IDisposable with
            member this.Dispose() =
                this.handle.Unreference()
                this.handle.Dispose()

    /// A way for objects outside of the loop to wakeup a state machine.
    /// 
    /// The `Notifier` is specific to a single machine.
    type Notifier(token: int64, wakeup: WakeupHandle) =
        member this.wakeup () =
            wakeup.queue.Enqueue(token)
            try wakeup.handle.Send()
                NotifyResponse.Ok
            with | :? NullReferenceException -> NotifyResponse.Retry(RetryNotifier(wakeup.handle))

    /// A way to retry sending a ping to a machine.
    /// 
    /// This does not queue the message subsequent times.
    and RetryNotifier(handle: UvAsyncHandle) =
        member this.wakeup () =
            try handle.Send()
                NotifyResponse.Ok
            with | :? NullReferenceException -> NotifyResponse.Retry(RetryNotifier(handle))

    /// The response from calling `wakeup` on a `Notifier`.
    /// 
    /// It can either indicate a successful result, or a failure with a `RetryNotifier`.
    and NotifyResponse =
        | Ok
        | Retry of RetryNotifier

    type Scope(token: int64, wakeup: WakeupHandle) =
        member this.notifier() = Notifier(token, wakeup)

    /// The base definition of a state machine.
    /// 
    /// State machines are generic over the context (`'c`) and scope which are inhected by the loop.
    type IMachine<'c> =
        /// Called when a machine is created by the loop.
        /// 
        /// This is not something you'll call yourself, prefer using a constructor when pre-building machines.
        abstract member create : 'c -> Scope -> Response
        /// Called when the socket associated with this machine is ready to operate on.
        abstract member ready : 'c -> Scope -> Response
        /// Called when a notification has been sent to this machine.
        abstract member wakeup : 'c -> Scope -> Response
        /// Called when a timer has expired.
        abstract member timeout : 'c -> Scope -> Response

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
        let libuv = Binding()
        let mutable loop = Idle(new UvLoopHandle(), new WakeupHandle(new UvAsyncHandle(), new ConcurrentQueue<int64>()))

        let mutable context: 'c = ctx
        let mutable machines: Map<int64, IMachine<'c>> = Map.empty

        let stop () =
            match loop with
            | Idle(_, _) ->         ()

            | Closed ->             ()

            | Running(l, wakeup) -> (wakeup :> IDisposable).Dispose()
                                    l.Stop()
                                    
                                    loop <- LoopState.Closed

        let remove token = 
            machines <- Map.remove token machines

            match machines.Count with
            | 0 ->  stop()
            | _ ->  ()

        let runOnce token f =
            match loop with
            | Closed -> ()
            | Idle(_, wakeup) | Running(_, wakeup) ->
                let scope = Scope(token, wakeup)
                let res = try f context scope with | e -> Error(e.Message)

                match res with
                | Done ->           remove token

                | Error(e) ->       printfn "Error running %i: '%s'" token e
                                    remove token

                | Deadline(t) ->    raise (NotImplementedException("Deadlines are not implemented"))

                | Response.Ok ->    ()

        /// Add a generic `IMachine<'c>` to the loop.
        /// 
        /// This can only be done if the loop is in the `Idle` state.
        member this.addMachine (f: Scope -> IMachine<'c>) =
            match loop with
            | Closed ->             raise (NotImplementedException("Adding machines to dirty loops is not implemented"))

            | Running(_, _) ->      ()

            | Idle(_, wakeup) ->    let token = int64 machines.Count
                                    let scope = Scope(token, wakeup)
                                    let machine = f scope

                                    machines <- Map.add token machine machines

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
                                
                                machines |> Map.iter(fun token machine -> runOnce token machine.create)

                                w.handle.Init(
                                    l, 
                                    System.Action(
                                        fun () -> 
                                            let rec handle () =
                                                match w.queue.TryDequeue() with
                                                | true, token ->    match (Map.tryFind token machines) with
                                                                    | Some(machine) -> runOnce token machine.wakeup
                                                                    | _ -> ()

                                                                    handle()

                                                | false, _ ->       ()

                                            handle()
                                    ), 
                                    null
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
    let run (l: Loop<'c>) = l.run() |> ignore
