module Build

open Fake.Core
open Fake.DotNet
open Fake.Tools
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.Api

// --------------------------------------------------------------------------------------
// Information about the project to be used at NuGet and in AssemblyInfo files
// --------------------------------------------------------------------------------------

let project = "Saturn.Cli"
let summary = "A dotnet CLI tool for Saturn projects providing code generation and scaffolding."

let gitOwner = "SaturnFramework"
let gitHome = "https://github.com/" + gitOwner
let gitName = "Saturn.Dotnet"

let gitUrl = gitHome + "/" + gitName

// --------------------------------------------------------------------------------------
// Build variables
// --------------------------------------------------------------------------------------

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let release = ReleaseNotes.parse (System.IO.File.ReadAllLines "../RELEASE_NOTES.md")

let packageDir = __SOURCE_DIRECTORY__ </> "out"
let buildDir = __SOURCE_DIRECTORY__ </> "temp"


// --------------------------------------------------------------------------------------
// Helpers
// --------------------------------------------------------------------------------------
let isNullOrWhiteSpace = System.String.IsNullOrWhiteSpace

let runTool cmd args workingDir =
    let arguments = args |> String.split ' ' |> Arguments.OfArgs
    let r =
        Command.RawCommand (cmd, arguments)
        |> CreateProcess.fromCommand
        |> CreateProcess.withWorkingDirectory workingDir
        |> Proc.run
    if r.ExitCode <> 0 then
        failwithf "Error while running '%s' with args: %s" cmd args

let getBuildParam = Environment.environVar

let DoNothing = ignore

let initializeContext args =
    let execContext = Context.FakeExecutionContext.Create false "build.fsx" args
    Context.setExecutionContext (Context.RuntimeContext.Fake execContext)

// --------------------------------------------------------------------------------------
// Build Targets
// --------------------------------------------------------------------------------------

let init args =
    initializeContext args
    Target.create "Clean" (fun _ ->
        Shell.cleanDirs [buildDir; packageDir]
    )

    Target.create "AssemblyInfo" (fun _ ->
        let getAssemblyInfoAttributes projectName =
            [
                AssemblyInfo.Title projectName
                AssemblyInfo.Product project
                AssemblyInfo.Description summary
                AssemblyInfo.Version release.AssemblyVersion
                AssemblyInfo.FileVersion release.AssemblyVersion
            ]

        let getProjectDetails (projectPath: string) =
            let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
            ( projectPath,
            projectName,
            System.IO.Path.GetDirectoryName(projectPath),
            (getAssemblyInfoAttributes projectName)
            )

        !! "../src/**/*.??proj"
        |> Seq.map getProjectDetails
        |> Seq.iter (fun (projFileName, _, folderName, attributes) ->
            match projFileName with
            | proj when proj.EndsWith("fsproj") -> AssemblyInfoFile.createFSharp (folderName </> "AssemblyInfo.fs") attributes
            | proj when proj.EndsWith("csproj") -> AssemblyInfoFile.createCSharp ((folderName </> "Properties") </> "AssemblyInfo.cs") attributes
            | proj when proj.EndsWith("vbproj") -> AssemblyInfoFile.createVisualBasic ((folderName </> "My Project") </> "AssemblyInfo.vb") attributes
            | _ -> ()
            )
    )

    Target.create "Restore" (fun _ ->
        DotNet.restore id ""
    )

    Target.create "Build" (fun _ ->
        DotNet.build id ""
    )

    Target.create "Publish" (fun _ ->
        DotNet.publish (fun p -> {p with OutputPath = Some buildDir}) ""
    )

    // --------------------------------------------------------------------------------------
    // Release Targets
    // --------------------------------------------------------------------------------------

    Target.create "Pack" (fun _ ->

        //Pack Saturn global tool
        Environment.setEnvironVar "Version" release.NugetVersion
        Environment.setEnvironVar "PackageVersion" release.NugetVersion

        Environment.setEnvironVar "Authors" "Krzysztof Cieslak"
        Environment.setEnvironVar "Copyright" "Copyright 2018-2022 Lambda Factory"
        Environment.setEnvironVar "Description" summary

        Environment.setEnvironVar "PackageReleaseNotes" (release.Notes |> String.toLines)
        Environment.setEnvironVar "PackageTags" "F#, Saturn, scaffolding"
        Environment.setEnvironVar "PackageProjectUrl" gitUrl
        Environment.setEnvironVar "RepositoryUrl" gitUrl
        Environment.setEnvironVar "PackageIconUrl" "https://avatars0.githubusercontent.com/u/35305523"
        Environment.setEnvironVar "PackageLicenseExpression" "MIT"

        DotNet.pack (fun p ->
            { p with
                OutputPath = Some packageDir
                Configuration = DotNet.BuildConfiguration.Release
            }) ""
    )

    Target.create "ReleaseGitHub" (fun _ ->
        let remote =
            Git.CommandHelper.getGitResult "" "remote -v"
            |> Seq.filter (fun (s: string) -> s.EndsWith("(push)"))
            |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
            |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

        Git.Staging.stageAll ""
        Git.Commit.exec "" (sprintf "Bump version to %s" release.NugetVersion)
        Git.Branches.pushBranch "" remote "master"


        Git.Branches.tag "" release.NugetVersion
        Git.Branches.pushTag "" remote release.NugetVersion

        let client =
            let user =
                match getBuildParam "github-user" with
                | s when not (isNullOrWhiteSpace s) -> s
                | _ -> UserInput.getUserInput "Username: "
            let pw =
                match getBuildParam "github-pw" with
                | s when not (isNullOrWhiteSpace s) -> s
                | _ -> UserInput.getUserPassword "Password: "

            // Git.createClient user pw
            GitHub.createClient user pw
        let files = !! (packageDir </> "*.nupkg")

        // release on github
        let cl =
            client
            |> GitHub.draftNewRelease gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
        (cl,files)
        ||> Seq.fold (fun acc e -> acc |> GitHub.uploadFile e)
        |> GitHub.publishDraft//releaseDraft
        |> Async.RunSynchronously
    )

    Target.create "Push" (fun _ ->
        let key =
            match getBuildParam "nuget-key" with
            | s when not (isNullOrWhiteSpace s) -> s
            | _ -> UserInput.getUserPassword "NuGet Key: "
        Paket.push (fun p -> { p with WorkingDir = buildDir; ApiKey = key; ToolType = ToolType.CreateLocalTool() }))

    // --------------------------------------------------------------------------------------
    // Build order
    // --------------------------------------------------------------------------------------
    Target.create "Default" DoNothing
    Target.create "Release" DoNothing

    let dependences =
        [
            "Clean" ==> "AssemblyInfo" ==> "Restore" ==> "Build" ==> "Default"
            "Default" ==> "Publish" ==> "Pack" ==> "ReleaseGitHub" ==> "Push" ==> "Release"
        ]

    ()

[<EntryPoint>]
let main args =
    init ((args |> List.ofArray))

    try
        match args with
        | [| target |] -> Target.runOrDefaultWithArguments target
        | _ -> Target.runOrDefaultWithArguments "Pack"
        0
    with
    | e ->
        printfn "%A" e
        1