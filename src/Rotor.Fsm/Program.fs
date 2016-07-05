/// # `Rotor.Fsm`
///
/// Base state machine implementation for asynchronous io

//State: First Principals implementation

//TODO: Use KestrelThread as a base for building up this layer

//Need a loop construct that takes a sequence of machines, registers libuv handles for them and executes certain events
//Need an IMachine construct that defines the base operations available on a machine, and how they're linked to libuv events
//Need to close the loop when there are no active machines
//Need a Response enum with the fo

//LibuvMachine(Libuv stuff, Option<Machine>)
//When Option<Machine> is None, remove it
//When not Machines.Any m.Machine.isSome, close the loop
//Machines store their own state info, each op returns a new machine

namespace Rotor

module Fsm =
    open System
    open Rotor.Libuv

    type Response<'m> =
    | Ok of 'm
    | Done
    | Error of string
    | Deadline of 'm * TimeSpan

    //NOTE: This is going to require an ugly type cast, maybe we should pass a state arg?
    //This isn't really using F# effectively, we have the accessible state, but aren't using it on types. Needs help
    type IMachine<'c, 's> =
        abstract member Create :    IMachine<'c, 's> * 'c * 's -> Response<IMachine<'c, 's>>
        abstract member Ready :     IMachine<'c, 's> * 'c * 's -> Response<IMachine<'c, 's>>
        abstract member Wakeup :    IMachine<'c, 's> * 'c * 's -> Response<IMachine<'c, 's>>
        abstract member Timeout :   IMachine<'c, 's> * 'c * 's -> Response<IMachine<'c, 's>>