/// # `Rotor.Fsm.Sock`
///
/// Base state machine implementation for asynchronous io.

//State: First Principals implementation

//Check out: Microsoft.AspNetCore.Server.Kestrel.Internal.Http.Connection
//Check out: Microsoft.AspNetCore.Server.Kestrel.Internal.Http.SocketInput

//Build implementations for server and client

namespace Rotor.Fsm

module Sock =
    open System
    open Rotor.Libuv
    open Rotor.Libuv.Networking
    open Rotor.Fsm.Base

    /// The behaviour an `ISocketMachine<'c>` intends its parent to perform.
    /// 
    /// The possible intentions are:
    /// - `Read`: read bytes from the connection buffer.
    /// - `Write`: write bytes to the connection buffer.
    /// - `Flush`: wait for all bytes in the connection buffer to be processed.
    /// - `Sleep`: idle until a wakeup or timeout occurs.
    type Intent =
    | Read of ReadIntent
    | Write of byte []
    | Flush
    | Sleep
    and ReadIntent =
    | Num of uint32

    /// The base definition of a state machine that acts on socket events.
    [<AbstractClass>]
    type ISocketMachine<'c>() =
        /// Called when the connection is ready.
        /// 
        /// At this point, the connection is connected and ready to read/write.
        abstract member ready :     'c -> Scope -> Intent

        /// Called when the connection has data to read.
        abstract member read :      'c -> Scope -> Intent

        /// Called when the connection is ready to write to.
        abstract member write :     'c -> Scope -> Intent

        /// Called when the parent fsm is woken up.
        abstract member wakeup :    'c -> Scope -> Intent

        /// Called when the parent fsm times out.
        abstract member timeout :   'c -> Scope -> Intent

    /// A machine that manages a socket connection.
    /// 
    /// This fsm has a child `ISocketMachine` which provides the business logic for handling 
    /// io events.
    type Socket<'c>(addr: ServerAddress, m: ISocketMachine<'c>) =
        inherit IMachine<'c>()

        let mutable conn = new UvTcpHandle()
        let listen f = conn.Listen(1, System.Action<UvStreamHandle,int,exn,obj>(f), null) |> ignore

        override this.create c s =
            //Register and initialise the connection with the loop
            //This is also where we should register readiness callbacks and do mem pool stuff
            //That means the mem pool needs to be accessible to the scope

            //We want to handle accepting incoming connections ourselves, so the child machine
            //only acts on active connections.

            //Will also need to be careful about how the lifetime of the scope is dealt with
            //Passing it around to callbacks in here may have unexpected side effects.
            printfn "creating"

            s.register (fun l -> conn.Init(l, null))

            //TODO: Should probably be in child machine
            conn.Bind(addr)
            listen (fun stream status error state -> printfn "incoming connection")

            printfn "ready"

            Response.Done

        override this.wakeup c s =
            raise (NotImplementedException("check for idle, call wakeup on child"))

        override this.timeout c s =
            raise (NotImplementedException("check conn state, call timeout on child"))

        override this.dispose () =
            conn.Dispose()