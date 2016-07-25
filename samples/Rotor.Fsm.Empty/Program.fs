open Rotor.Fsm

//The smallest loop you can have. Doesn't do much...
[<EntryPoint>]
let main argv = (loop ()) |> run