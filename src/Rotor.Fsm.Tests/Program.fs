// Learn more about F# at http://fsharp.org

open System
open Xunit

module Tests =
    [<Fact>]
    let ItWorks() = 
        Assert.True(true)

    //Test:
    // - Empty loop run immediately closes
    // - Loop stops when FSM returns Done
    // - Loop stops when FSM returns Error
    // - Loop calls create when running
    // - Loop executes timeout (pre-allocated, just sets repeat?)
    // - Loop returns notifier for correct machine from scope