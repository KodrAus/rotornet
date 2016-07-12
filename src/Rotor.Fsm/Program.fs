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
    open Rotor.Libuv.Networking

    /// The kind of response returned by an invokation of a state machine.
    type Response =
    | Ok
    | Done
    | Error of string
    | Deadline of TimeSpan

    /// The base definition of a state machine.
    /// 
    /// State machines are generic over the context (`'c`) and scope (`'s`) which are inhected by the loop.
    type IMachine<'c, 's> =
        /// Called when a machine is created by the loop.
        /// 
        /// This is not something you'll call yourself, prefer using a constructor when pre-building machines.
        abstract member create :    'c -> 's -> Response
        /// Called when the socket associated with this machine is ready to operate on.
        abstract member ready :     'c -> 's -> Response
        /// Called when a notification has been sent to this machine.
        abstract member wakeup :    'c -> 's -> Response
        /// Called when a timer has expired.
        abstract member timeout :   'c -> 's -> Response

    //TODO: Implement the loop properly
    type Loop<'c, 's>(ctx, scope, machines) =
        /// A libuv binding and loop construct.
        let libuv = Binding()

        /// A shared context that all machines receive. It's mutable because each machine receives it one at a time.
        let mutable context = ctx
        /// A shared scope that lets machines interact with the underlying loop construc.
        let mutable scope = scope
        /// A list of machines that are bound to socket events.
        let mutable machines: IMachine<'c, 's> list = machines

        member this.run () =
            use loop = new UvLoopHandle()
            loop.Init(libuv)

            machines 
            |> List.iter(
                fun m -> 
                    let created = m.create context scope
                    match created with
                    | Ok -> let rec run (m: IMachine<_,_>) =
                                match (m.wakeup context scope) with
                                | Error e -> printfn "Error: %s" e
                                | Done -> printfn "Done"
                                | _ -> printfn "Still Working..."
                                       run m
                            run m
                    | _ -> ()
               )

            0

    /// Build a loop with the given arguments.
    let build c s m = Loop(c, s, m)

    /// Run a constructed loop.
    /// 
    /// This will block the calling thread until either the loop is stopped or all machines return a
    /// state of either `Done` or `Error`.
    let run (l: Loop<'c, 's>) = l.run()

    //TODO: Link IMachine to a libuv loop with socket events
    //Sockets need:
    // - Async to notify
    // - Ability to register timeouts
    // - Stream (or TCP) handle. These might be possible as base machine implementations

    //Also need to figure out how to test this