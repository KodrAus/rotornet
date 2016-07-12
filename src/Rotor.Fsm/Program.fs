/// # `Rotor.Fsm`
///
/// Base state machine implementation for asynchronous io.

//State: First Principals implementation

//TODO: Use KestrelThread as a base for building up this layer

//Need a loop construct that takes a sequence of machines, registers libuv handles for them and executes certain events
//Need an IMachine construct that defines the base operations available on a machine, and how they're linked to libuv events
//Need to close the loop when there are no active machines
//Need a Response enum with the fo

//LibuvMachine(Libuv stuff, Option<Machine>)
//When Option<Machine> is None, remove it
//When not Machines.Any m.Machine.isSome, close the loop

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
            queue.TryAdd(token)
            wakeup.Send()


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

    type Loop<'c>(ctx) =
        let libuv = Binding()
        let loop = new UvLoopHandle()

        //TODO: Capture this in struct
        let wakeup = new UvAsyncHandle()
        let wakeupQueue = new BlockingCollection<int64>()

        /// A shared context that all machines receive. It's safely mutable because each machine receives it one at a time.
        let mutable context: 'c = ctx
        /// A list of machines that are bound to socket events.
        let mutable machines: Map<int64, IMachine<'c>> = Map.empty

        member this.addMachine (f: Scope -> IMachine<'c>) =
            printfn "Adding machine"

            let token = int64 machines.Count
            let scope = Scope(token, wakeupQueue, wakeup)
            let machine = f scope

            machines <- Map.add token machine machines

        member this.run () =
            loop.Init(libuv)

            wakeup.Init(
                loop, 
                System.Action(
                    fun () -> 
                        wakeupQueue.GetConsumingEnumerable()
                        |> Seq.cast<int64>
                        |> Seq.iter(
                            fun (token: int64) ->
                                printfn "Wakeup setup"
                                match (Map.tryFind token machines) with
                                //TODO: Handle wakeup properly
                                | Some(m) -> 
                                    let scope = Scope(token, wakeupQueue, wakeup)
                                    (m.wakeup context scope) |> ignore
                                | _ -> ()
                        )
                ), 
                System.Action<_, _>(fun p1 p2 -> ())
            ) |> ignore

            match machines.Count with
            | 0 -> 0
            | _ -> loop.Run()

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