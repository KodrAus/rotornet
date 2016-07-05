/// # `Rotor.Fsm`
///
/// Base state machine implementation for asynchronous io

//TODO: Use KestrelThread as a base for building up this layer

namespace Rotor

module Fsm =
    open System
    open Rotor.Libuv

    let fsm = printfn "Hello"