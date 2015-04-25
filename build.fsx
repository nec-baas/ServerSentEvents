#r @"packages/FAKE/tools/FakeLib.dll"
open Fake
open Fake.AssemblyInfoFile
open System
open System.IO

// --------------------------------------------------------------------------------------
// Information about the project to be used at NuGet and in AssemblyInfo files
// --------------------------------------------------------------------------------------

let project = "ServerSentEvents"
let authors = ["Kwang Yul Seo"]
let summary = "A Server-Sent Events client implementation for .NET"
let description = """
ServerSentEvents is a client library for Server-Sent Events (SSE).
"""
let tags = "SSE ServerSentEvents EventSource"

let release =
    File.ReadLines "RELEASE_NOTES.md"
    |> ReleaseNotesHelper.parseReleaseNotes

let isAppVeyorBuild = environVar "APPVEYOR" <> null
let nugetVersion =
    if isAppVeyorBuild then sprintf "%s-a%s" release.NugetVersion (DateTime.UtcNow.ToString "yyMMddHHmm")
    else release.NugetVersion

// --------------------------------------------------------------------------------------
// Generate assembly info files with the right version & up-to-date information
// --------------------------------------------------------------------------------------

Target "AssemblyInfo" <| fun () ->
    CreateCSharpAssemblyInfo "./ServerSentEvents/Properties/AssemblyInfo.cs"
       [ Attribute.Title project
         Attribute.Product project
         Attribute.Description summary
         Attribute.Version release.AssemblyVersion
         Attribute.FileVersion release.AssemblyVersion]

// --------------------------------------------------------------------------------------
// Build library & test projects
// --------------------------------------------------------------------------------------

Target "Build" (fun () ->
    !! "ServerSentEvents/ServerSentEvents.csproj"
      |> MSBuildRelease "" "Build"
      |> Log "AppBuild-Output: "
)

Target "BuildSample" (fun () ->
    !! "ServerSentEvents.Sample/ServerSentEvents.Sample.csproj"
      |> MSBuildRelease "" "Build"
      |> Log "AppBuild-Output: "
)

// --------------------------------------------------------------------------------------
// Help
// --------------------------------------------------------------------------------------

Target "Help" (fun () ->
    printfn ""
    printfn "  Please specify the target by calling 'build <Target>'"
    printfn ""
    printfn "  Targets for building:"
    printfn "  * Build"
    printfn "  * BuildSample"
)

"AssemblyInfo"
  ==> "Build"
  ==> "BuildSample"

RunTargetOrDefault "Help"
