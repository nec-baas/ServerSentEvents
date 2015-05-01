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

Target "BuildTests" (fun () ->
    !! "ServerSentEvents.Tests/ServerSentEvents.Tests.csproj"
      |> MSBuildRelease "" "Build"
      |> Log "AppBuild-Output: "
)

Target "NUnit" (fun () ->
    !! ("ServerSentEvents.Tests/bin/Release/ServerSentEvents.Tests.dll")
    |> NUnit (fun p -> { p with DisableShadowCopy = true; OutputFile = "TestResult.xml" } )
)


Target "NuGet" (fun () ->
    FileHelper.CreateDir "bin"

    // Format the release notes
    let releaseNotes = release.Notes |> String.concat "\n"

    NuGet (fun p ->
        {p with
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = nugetVersion
            ReleaseNotes = releaseNotes
            Tags = tags
            OutputPath = "bin"
            // This option is needed to workaround a bug in NuGet
            // Otherwise, NuGet fails to recognize dotted paths in nuspec file.
            // https://nuget.codeplex.com/workitem/2111
            NoDefaultExcludes = true
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Files = [("../ServerSentEvents/bin/Release/ServerSentEvents.dll", Some "lib/net45", None)]})
        "NuGet/ServerSentEvents.nuspec"
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
    printfn "  * BuildTests"
    printfn "  * NUnit"
    printfn "  * NuGet"
)

"AssemblyInfo"
  ==> "Build"
  ==> "BuildSample"
  ==> "BuildTests"
  ==> "NUnit"
  ==> "NuGet"

RunTargetOrDefault "Help"
