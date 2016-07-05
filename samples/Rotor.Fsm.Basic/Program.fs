//TODO: Write a machine that ticks 3 times, then ends

open Rotor.Fsm

[<EntryPoint>]
let main argv = 
    //Pretty much the API we want to be using
    //Will need to figure out the self returns for object expressions
    //Worst case we just won't be able to use them
    let l = seq {
        new IMachine with
            member m.Create c s =
                printfn "Created"
                Some(Ok(m))
            member m.Ready c s =
                printfn "Ready"
                Some(Ok(m))
            member m.Wakeup c s =
                printfn "Wakeup"
                Some(Ok(m))
            member m.Timeout c s =
                printfn "Timeout"
                Some(Ok(m))
    } 
    |> loop 
    |> run

    0
