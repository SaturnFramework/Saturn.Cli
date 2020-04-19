module Program
open System.IO
open System
open System.Diagnostics

open Models
open Files

let runMigration (extraArgv: string []) =
    let baseArgumetns = "run --project src/Migrations/Migrations.fsproj"
    let arguments =
        match extraArgv with
        | [||] -> baseArgumetns
        | _ ->
            sprintf "%s -- %s" baseArgumetns (Array.fold (+) "" extraArgv)
    let startInfo = ProcessStartInfo()
    startInfo.CreateNoWindow <- true
    startInfo.Arguments <- arguments
    startInfo.FileName <- "dotnet"
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.RedirectStandardInput <- true
    startInfo.WorkingDirectory <- Directory.GetCurrentDirectory()
    let processs = Process.Start startInfo
    let output = processs.StandardOutput.ReadToEnd()
    processs.WaitForExit()
    printfn "%s" output

let printHelp () =
    Console.ForegroundColor <- ConsoleColor.Cyan
    printfn "Welcome to Saturn CLI. Avaliable commands:"
    Console.ResetColor ()
    [
        "* gen, gen.html", "generates the model, data access layer, controller and server side views"
        "* gen.json", "generates the model, data access layer and controller returning data in JSON format"
        "* gen.model", "generates the model and data access layer only"
        "* migration", "runs all migrations, updating the database to the latest version"
        "* interactive", "[experimental] starts interactive mode to interactively explore the running application"
    ]
    |> List.iter (fun (command, description) -> printfn "%-16s %s" command description)

[<EntryPoint>]
let main argv =
    match Array.tryHead argv |> Option.map (fun n -> n.ToLower()) with
    | Some action ->
        try
            let flags, argv = argv |> Array.partition (fun f -> f.StartsWith "--")
            match action with
            | "gen" | "gen.html" ->
                let name = argv.[1]
                let names = argv.[2]
                let fields = argv.[3 ..] |> Array.map (fun n -> let x = n.Split(':', 2) in {name = x.[0]; typ = ParameterType.TryParse x.[1]; nullable = false})
                CodeGeneration.generateHtml name names fields
            | "gen.json" ->
                let name = argv.[1]
                let names = argv.[2]
                let fields = argv.[3 ..] |> Array.map (fun n -> let x = n.Split(':', 2) in {name = x.[0]; typ = ParameterType.TryParse x.[1]; nullable = false})
                CodeGeneration.generateJson name names fields
            | "gen.model" ->
               let name = argv.[1]
               let names = argv.[2]
               let fields = argv.[3 ..] |> Array.map (fun n -> let x = n.Split(':', 2) in {name = x.[0]; typ = ParameterType.TryParse x.[1]; nullable = false})
               CodeGeneration.generateMdl name names fields
            // | "gen.graphql" -> generateGraphQL name names fields
            | "migration" -> runMigration argv.[1 ..]
            | "interactive" -> Interactive.start (Directory.GetCurrentDirectory())
            | "help" | "--help" | "-?" -> printHelp ()
            | _ ->
                printfn "Wrong format of input parameters - they should be passed in following format: Command Name Names [field_name:field_type]"
                printfn "---"
                printHelp ()
        with
        | ex ->
            printfn "%s" ex.Message
            printfn "---"
            printHelp ()
    | None ->
        printHelp ()

    0 // return an integer exit code
