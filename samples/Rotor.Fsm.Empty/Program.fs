open System
open Rotor.Fsm

[<EntryPoint>]
let main argv = 
    let l = (loop ())
    l |> run
    0