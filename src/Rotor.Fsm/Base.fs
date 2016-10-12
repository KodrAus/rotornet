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
    open Rotor.Libuv.Infrastructure

    /// The kind of response returned by an invokation of a state machine.
    /// 
    /// The possible responses are:
    /// - `Ok`: move into an idle state and wait for a wakeup or deadline.
    /// - `Done`: close the machine and any resources it holds because it's finished.
    /// - `Error`: close the machine and any resources it holds because it broke.
    /// - `Deadline`: set a timeout on the machine.
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
        member this.init l f =   handle.Init(l, System.Action(f), null) |> ignore
        member this.notifier t = Notifier(t, handle, queue)
        member this.dequeue =    queue.TryDequeue()
        member this.length =     queue.Count
        member this.wakeup =     try handle.Send()
                                 with | :? ObjectDisposedException -> ()

        interface IDisposable with
            member this.Dispose () =
                handle.Reference()
                handle.Dispose()

    [<Struct>]
    type EarlyScope(token: int64, loop: UvLoopHandle, wakeup: WakeupHandle) =
        member this.notifier() = wakeup.notifier token

    [<Struct>]
    type Scope(token: int64, loop: UvLoopHandle, wakeup: WakeupHandle, memory: MemoryPool) =
        member this.notifier() =    wakeup.notifier token
        member this.register f =    f(loop)
        member this.leaseBlock s =  memory.Lease()
        member this.returnBlock m = memory.Return(m)

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
            member this.Dispose() = this.dispose ()

    /// An internal wrapper for all machine types
    type private MachineState =
    | Uninitialised
    | Running

    [<Struct>]
    type private Machine<'c> =
        val m: IMachine<'c>
        val t: UvTimerHandle
        val mutable s: MachineState

        new (m, t) = { m = m; s = MachineState.Uninitialised; t = t }

        member this.init l f = this.t.Init(l, System.Action(f), null) |> ignore
                               this.s <- MachineState.Running

        member this.machine = this.m

        member this.state = this.s

        member this.timeout ms = this.t.Start(ms, 0UL)

        interface IDisposable with
            member this.Dispose() = (this.m :> IDisposable).Dispose()
                                    this.t.Dispose()

    type private LoopState =
    | Idle of UvLoopHandle * WakeupHandle
    | Running of UvLoopHandle * WakeupHandle * MemoryPool
    | Closed

    /// The main IO loop made up of state machines.
    /// 
    /// The loop manages the lifetimes of the machines and bound io and gives you a means to communicate
    /// from outside the loop itself.
    /// 
    /// When `run` is called, the loop will block the calling thread until complete.
    type Loop<'c>(ctx) =
        let libuv =                     Binding()
        let mutable loop =              Idle(new UvLoopHandle(),
                                             new WakeupHandle(new UvAsyncHandle(),
                                                              new ConcurrentQueue<int64>()))
        let mutable context =           ctx
        let mutable machines:
            Map<int64, Machine<'c>> =   Map.empty

        let stop () =
            match loop with
            | Running(handle, wakeup, memory) -> handle.Stop()

                                                 (wakeup :> IDisposable).Dispose()
                                                 (handle :> IDisposable).Dispose()
                                                 (memory :> IDisposable).Dispose()

                                                 loop <- LoopState.Closed

            | Idle(_, _) ->                      ()

            | Closed ->                          ()

        let remove token =
            let machine = Map.find token machines
            (machine :> IDisposable).Dispose()

            machines <- Map.remove token machines

            match machines.Count with
            | 0 -> stop()
            | _ -> ()

        let runOnce token (fsm: Machine<'c>) f =
            match loop with
            | Running(loop, wakeup, memory) -> let scope = Scope(token, loop, wakeup, memory)
                                               let res = try f context scope with | e -> Error(e.Message)

                                               match res with
                                               | Done ->        remove token

                                               | Error(e) ->    printfn "Error running %i: '%s'" token e
                                                                remove token

                                               | Deadline(t) -> fsm.timeout t

                                               | Response.Ok -> ()

            | Closed ->                        ()

            | Idle(_, _) ->                    ()
            

        /// Add a generic `IMachine<'c>` to the loop.
        /// 
        /// This can only be done if the loop is in the `Idle` state.
        member this.addMachine (f: EarlyScope -> 'a) =
            match loop with
            | Idle(loop, wakeup) -> let token = int64 machines.Count
                                    let scope = EarlyScope(token, loop, wakeup)
                                    let machine = f scope

                                    let machine = new Machine<'c>((machine :> IMachine<'c>), new UvTimerHandle())
                                    machines <- machines |> Map.add token machine

            | Closed ->             raise (NotImplementedException("Adding machines to dirty loops is not implemented"))

            | Running(_, _, _) ->   raise (NotImplementedException("Adding machines to running loops is not implemented"))

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
            | Idle(handle, wakeup) -> let memory = new MemoryPool()
                                      loop <- LoopState.Running(handle, wakeup, memory)
                                      handle.Init(libuv)

                                      let create(token: int64, fsm: Machine<'c>) = fsm.init handle (fun () -> runOnce token fsm fsm.machine.timeout)
                                                                                   runOnce token fsm fsm.machine.create

                                      //Deqeue at most l items
                                      //This is to make sure other events this loop iteration get a chance to run
                                      let rec dequeueAtMost l =  match l with
                                                                 | 0 -> ()

                                                                 | _ -> match wakeup.dequeue with
                                                                        | true, token -> match (Map.tryFind token machines) with
                                                                                         | Some(fsm) -> match fsm.state with
                                                                                                        //Wakeups may get called on Uninitialised machines
                                                                                                        //We only run this check for wakeups
                                                                                                        | MachineState.Uninitialised -> create(token, fsm)
                                                                                                        | MachineState.Running -> ()

                                                                                                        runOnce token fsm fsm.machine.wakeup
                                                                                         | _ -> ()

                                                                                         dequeueAtMost (l - 1)

                                                                        | false, _ ->    ()

                                      //Initialise wakeup mechanism
                                      let handleWakeup () = dequeueAtMost (wakeup.length)

                                                            match wakeup.length with
                                                            | 0 -> ()
                                                            | _ -> wakeup.wakeup

                                      wakeup.init handle handleWakeup

                                      //Initialise machines and timer callbacks
                                      machines |> Map.iter(fun token fsm -> create(token, fsm))

                                      match machines.Count with
                                      | 0 -> 0
                                      | _ -> handle.Run()

            | Closed ->               raise (NotImplementedException("Restarting dirty loops is not implemented"))

            | Running(_, _, _) ->     0

    /// Build a loop with the given arguments.
    let loop c = Loop(c)

    /// Run a constructed loop.
    /// 
    /// This will block the calling thread until either the loop is stopped or all machines return a
    /// state of either `Done` or `Error`.
    let run (l: Loop<'c>) = l.run()
