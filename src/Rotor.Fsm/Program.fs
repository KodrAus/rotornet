﻿/// # `Rotor.Fsm`
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
    open Rotor.Libuv

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
        abstract member Create :    'c -> 's -> Response
        /// Called when the socket associated with this machine is ready to operate on.
        abstract member Ready :     'c -> 's -> Response
        /// Called when a notification has been sent to this machine.
        abstract member Wakeup :    'c -> 's -> Response
        /// Called when a timer has expired.
        abstract member Timeout :   'c -> 's -> Response