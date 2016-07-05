//TODO: Write a machine that ticks 3 times, then ends
//TODO: Work out how to return the intent

open Rotor.Fsm

type Counter<'c, 's> (state) =
    member this.State = state

    interface IMachine<'c, 's> with
        //When a machine is created internally for a socket
        //This is not something you call yourself
        //In this case, we construct the machine ourselves so don't need this
        member this.Create c s =
            done

        //When a machine is ready to run
        //This corresponds to a socket ready call
        member this.Ready m c s =
            printfn "Ready"
            deadline Counter(3) 1000

        //When a machine receives a wakeup
        member this.Wakeup m c s =
            done

        //When a machine timeout occurs
        member this.Timeout m c s =
            match m.State with
            | x when x < 0 -> error "state was less than 0" //We expect the counter to be greater than 0
            | 0 -> done //If the counter is less than 0, we close this machine
            | x -> deadline Counter(x - 1) 1000 //If the counter is greater than 0, we continue

[<EntryPoint>]
let main argv = 
    //Pretty much the API we want to be using
    //Will need to figure out the self returns for object expressions
    //Worst case we just won't be able to use them
    seq {
        Counter(3)
    } 
    |> loop 
    |> run

    0
