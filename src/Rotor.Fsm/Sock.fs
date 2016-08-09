/// # `Rotor.Fsm.Sock`
///
/// Base state machine implementation for asynchronous io.

//State: First Principals implementation

namespace Rotor.Fsm

module Sock =
    open System
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
        /// Called when the connection is idle.
        /// 
        /// At this point, the connection is connected and ready to read/write.
        abstract member idle :     'c -> Scope -> Intent

        /// Called when the connection has data to read.
        abstract member read :     'c -> Scope -> Intent

        /// Called when the connection is ready to write to.
        abstract member write :    'c -> Scope -> Intent

        /// Called when the parent fsm is woken up.
        abstract member wakeup :   'c -> Scope -> Intent

        /// Called when the parent fsm times out.
        abstract member timeout :  'c -> Scope -> Intent

    /// A machine that manages a socket connection.
    /// 
    /// This fsm has a child `ISocketMachine` which provides the business logic for handling 
    /// io events.
    type Socket<'c>(m: ISocketMachine<'c>) =
        inherit IMachine<'c>()

        override this.create c s =
            raise (NotImplementedException("connect socket, wire up idle on ready"))

        override this.wakeup c s =
             raise (NotImplementedException("check for idle, call wakeup on child"))

        override this.timeout c s =
             raise (NotImplementedException("check conn state, call timeout on child"))
        
        override this.dispose () =
            ()