#if BOOT

open Fake
module FB = Fake.Boot
FB.Prepare {
    FB.Config.Default __SOURCE_DIRECTORY__ with
        NuGetDependencies =
            let ( ! ) x = FB.NuGetDependency.Create x
            [
                { !"jQuery" with Version = FB.SemanticVersion "1.9.1" }
                !"DotNetZip"
                !"IntelliFactory.Build"
                !"IntelliFactory.FastInvoke"
                !"IntelliFactory.Xml"
                !"Mono.Cecil"
                !"YUICompressor.NET"
            ]
}

#else
#load ".build/boot.fsx"
open System
open System.IO
open System.Net
open System.Text
open Fake
open Ionic.Zip

module B = IntelliFactory.Build.CommonBuildSetup
module F = IntelliFactory.Build.FileSystem
module NG = IntelliFactory.Build.NuGetUtils
module VP = IntelliFactory.Build.VsixPackages
module VST = IntelliFactory.Build.VSTemplates
module VX = IntelliFactory.Build.VsixExtensions
module X = IntelliFactory.Build.XmlGenerator

let ( +/ ) a b = Path.Combine(a, b)
let RootDir = __SOURCE_DIRECTORY__
let T x f = Target x f; x
let DotBuildDir = RootDir +/ ".build"
let PackagesDir = RootDir +/ "packages"
let ToolsDir = PackagesDir +/ "tools"

module Config =

    let PackageId = "WebSharper"
    let AssemblyVersion = Version "2.5"
    let VersionSuffix = "alpha"
    let NuGetVersion = NG.ComputeVersion PackageId (global.NuGet.SemanticVersion(AssemblyVersion, VersionSuffix))
    let FileVersion = NuGetVersion.Version

    let Company = "IntelliFactory"
    let Description = "F#-to-JavaScript compiler and web application framework"
    let LicenseUrl = "http://websharper.com/licensing"
    let Tags = ["Web"; "JavaScript"; "F#"]
    let Website = "http://bitbucket.org/IntelliFactory/websharper"
    let Icon = VST.Icon.FromFile (RootDir +/ "WebSharper.png")

let Metadata =
    let m = B.Metadata.Create()
    m.AssemblyVersion <- Some Config.AssemblyVersion
    m.Author <- Some Config.Company
    m.Description <- Some Config.Description
    m.FileVersion <- Some Config.FileVersion
    m.Product <- Some Config.PackageId
    m.VersionSuffix <- Some Config.VersionSuffix
    m.Website <- Some Config.Website
    m

[<AutoOpen>]
module Extensions =

    type B.BuildConfiguration with
        static member Release(v: B.FrameworkVersion)(deps: list<string>) : B.BuildConfiguration =
            {
                ConfigurationName = "Release"
                Debug = false
                FrameworkVersion = v
                NuGetDependencies =
                    new global.NuGet.PackageDependencySet(
                        v.ToFrameworkName(),
                        [for d in deps -> global.NuGet.PackageDependency(d)]
                    )
            }

    type B.Solution with

        static member Standard(rootDir: string)(m: B.Metadata)(ps: list<string -> B.Project>) : B.Solution =
            {
                Metadata = m
                Projects = [for p in ps -> p rootDir]
                RootDirectory = rootDir
            }

        member this.BuildSync(?opts: B.MSBuildOptions) =
            this.MSBuild(?options=opts)
            |> Async.RunSynchronously

        member this.CleanSync(?opts: B.MSBuildOptions) =
            let opts : B.MSBuildOptions =
                match opts with
                | Some opts ->
                    { opts with Targets = ["Clean"] }
                | None ->
                    {
                        BuildConfiguration = None
                        Properties = Map.empty
                        Targets = ["Clean"]
                    }
            this.MSBuild opts
            |> Async.RunSynchronously

    type B.Project with

        static member FSharp(name: string)(configs: list<B.BuildConfiguration>)(rootDir: string) : B.Project =
            {
                Name = name
                MSBuildProjectFilePath = Some (rootDir +/ name +/ (name + ".fsproj"))
                BuildConfigurations = configs
            }

        static member CSharp(name: string)(configs: list<B.BuildConfiguration>)(rootDir: string) : B.Project =
            {
                Name = name
                MSBuildProjectFilePath = Some (rootDir +/ name +/ (name + ".csproj"))
                BuildConfigurations = configs
            }

let RawJavaScriptFiles =
    [
        RootDir +/ "IntelliFactory.JavaScript" +/ "Runtime.js"
        RootDir +/ "IntelliFactory.WebSharper" +/ "Json.js"
    ]

let CompressJavaScript = T "CompressJavaScript" <| fun () ->
    let jc = Yahoo.Yui.Compressor.JavaScriptCompressor()
    for i in RawJavaScriptFiles do
        let cj =
            File.ReadAllText(i)
            |> jc.Compress
        let p = Path.ChangeExtension(i, ".min.js")
        F.TextContent(cj).WriteFile(p)

let CleanCompressedJavaScript = T "CleanCompressedJavaScript" <| fun () ->
    for i in RawJavaScriptFiles do
        let p = Path.ChangeExtension(i, ".min.js")
        File.Delete p

let Deps =
    [
        "IntelliFactory.FastInvoke"
        "IntelliFactory.Xml"
        "Mono.Cecil"
    ]

let C35 = B.BuildConfiguration.Release B.Net35 Deps
let C40 = B.BuildConfiguration.Release B.Net40 Deps

let Configs = [C35; C40]

let CompilerSolution : B.Solution =
    B.Solution.Standard RootDir Metadata [
        B.Project.FSharp "IntelliFactory.JavaScript" Configs
        B.Project.FSharp "IntelliFactory.JavaScript.Tests" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Core" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Compiler" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.InterfaceGenerator" Configs
        B.Project.FSharp "WebSharper" Configs
    ]

let BuildCompiler = T "BuildCompiler" CompilerSolution.BuildSync
let CleanCompiler = T "CleanCompiler" CompilerSolution.CleanSync

let MainSolution =
    B.Solution.Standard RootDir Metadata [
        B.Project.FSharp "IntelliFactory.WebSharper" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Dom" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.JQuery" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Collections" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Collections.Tests" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Control" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Ecma" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Testing" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Tests" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Web" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Web.Tests" Configs
        B.Project.FSharp "IntelliFactory.Reactive" Configs
        B.Project.FSharp "IntelliFactory.Formlet" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Formlet" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Formlet.Tests" Configs
        B.Project.FSharp "IntelliFactory.Html" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Html" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Html5" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Html5.Tests" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Sitelets" Configs
        B.Project.FSharp "IntelliFactory.WebSharper.Sitelets.Tests" Configs
    ]

let BuildMain = T "BuildMain" MainSolution.BuildSync
let CleanMain = T "CleanMain" MainSolution.CleanSync

let SiteOptions =
    match environVarOrNone "WebOutDir" with
    | None | Some "" | Some null | Some "." -> None
    | Some d ->
        let opts : B.MSBuildOptions =
            {
                BuildConfiguration = None
                Properties = Map ["OutDir", d]
                Targets = ["Build"]
            }
        Some opts

let SiteSolution =
    B.Solution.Standard __SOURCE_DIRECTORY__ Metadata [
        B.Project.FSharp "Website" [C40]
        B.Project.CSharp "Web" [C40]
    ]

let BuildSite = T "BuildSite" (fun () -> SiteSolution.BuildSync(?opts=SiteOptions))
let CleanSite = T "CleanSite" (fun () -> SiteSolution.CleanSync(?opts=SiteOptions))

let Build = T "Build" ignore
let Clean = T "Clean" ignore

let BuildWebSharperTargetsXml () =
    let ns = "http://schemas.microsoft.com/developer/msbuild/2003"
    let e n = X.Element.Create(n, ns)
    let ( -- ) (a: X.Element) (b: string) = X.Element.WithText b a
    e "Project" - [
        e "PropertyGroup" - [
            yield e "WebSharperVersion" -- string Config.NuGetVersion
            let variants =
                [
                    "$(MSBuildProjectDirectory)/../packages"
                    "$(MSBuildProjectDirectory)/../../packages"
                    "$(MSBuildProjectDirectory)/../../../packages"
                ]
            for variant in variants do
                yield
                    e "PackagesFolder"
                        + [
                            "Condition",
                                sprintf " '$(PackagesFolder)' == '' AND Exists('%s')" variant
                        ]
                    -- variant
            let homeVariants =
                [
                    "v2.0", "net35"
                    "v3.0", "net35"
                    "v3.5", "net35"
                    "v4.0", "net40"
                    "v4.5", "net40"
                ]
            for (tfv, home) in homeVariants do
                yield
                    e "WebSharperHome"
                        + [
                            "Condition",
                                sprintf " '$(TargetFrameworkVersion)' == '%s' " tfv
                        ]
                        -- sprintf "$(PackagesFolder)/WebSharper.$(WebSharperVersion)/tools/%s" home
        ]
        e "Import" + ["Project", "$(WebSharperHome)/WebSharper.targets"]
    ]

let BuildWebSharperTargets =
    T "BuildWebSharperTargets" <| fun () ->
        ensureDirectory DotBuildDir
        let targets = DotBuildDir +/ "WebSharper.targets"
        X.WriteFile targets (BuildWebSharperTargetsXml ())

let BuildWebConfigTransformXml () =
    let e n = X.Element.Create n
    e "configuration" - [
        e "system.webServer" - [
            e "modules" - [
                e "add" + [
                    "name", "WebSharper.RemotingModule"
                    "type", "IntelliFactory.WebSharper.Web.RpcModule, IntelliFactory.WebSharper.Web"
                ]
                e "add" + [
                    "name", "WebSharper.Sitelets"
                    "type", "IntelliFactory.WebSharper.Sitelets.HttpModule, IntelliFactory.WebSharper.Sitelets"
                ]
            ]
        ]
    ]

let BuildWebConfigTransform =
    T "BuildWebConfigTransform" <| fun () ->
        ensureDirectory DotBuildDir
        let t = DotBuildDir +/ "Web.config.transform"
        X.WriteFile t (BuildWebConfigTransformXml ())


let NuGetPackageFile =
    DotBuildDir +/ sprintf "%s.%O.nupkg" Config.PackageId Config.NuGetVersion

//let BuildNuSpecXml () =
//    let e n = X.Element.Create n
//    let ( -- ) (a: X.Element) (b: string) = X.Element.WithText b a
//    e "package" - [
//        e "metadata" - [
//            e "id" -- Config.PackageId
//            e "version" -- Config.Version
//            e "authors"-- Config.Company
//            e "owners"-- Config.Company
//            e "licenseUrl" -- Config.LicenseUrl
//            e "projectUrl"-- Config.Website
//            e "requireLicenseAcceptance" -- "false"
//            e "description" -- Config.Description
//            e "copyright" -- sprintf "Copyright (c) %O %s" DateTime.Now.Year Config.Company
//            e "tags" -- String.concat " " Config.Tags
//        ]
//        e "files" - [
//            e "file" + ["src", "WebSharper.targets"; "target", "content"]
//            e "file" + ["src", "Web.config.transform"; "target", "content"]
//            e "file" + ["src", @"root\net35\*.*"; "target", @"tools\net35"]
//            e "file" + ["src", @"root\net40\*.*"; "target", @"tools\net40"]
//        ]
//    ]

/// TODO: helpers for buliding packages from a solution spec.
let BuildNuGet = T "BuildNuGet" <| fun () ->
    let content =
        use out = new MemoryStream()
        let builder = new NuGet.PackageBuilder()
        builder.Id <- Config.PackageId
        builder.Version <- Config.NuGetVersion
        builder.Authors.Add(Config.Company) |> ignore
        builder.Owners.Add(Config.Company) |> ignore
        builder.LicenseUrl <- Uri(Config.LicenseUrl)
        builder.ProjectUrl <- Uri(Config.Website)
        builder.Copyright <- String.Format("Copyright (c) {0} {1}", DateTime.Now.Year, Config.Company)
        builder.Description <- Config.Description
        Config.Tags
        |> Seq.iter (builder.Tags.Add >> ignore)

        for c in Configs do
            let config = "Release-" + c.FrameworkVersion.GetMSBuildLiteral()
            let prefix = RootDir +/ "*" +/ "bin" +/ config
            (!+ (prefix +/ "*.dll")
                ++ (prefix +/ "*.xml")
                ++ (prefix +/ "*.exe")
                ++ (prefix +/ "*.exe.config")
                ++ (RootDir +/ "build" +/ "DeployedTargets" +/ "WebSharper.targets"))
            |> Scan
            |> Seq.filter (fun x ->
                [
                    "generator.exe"
                    "generator.exe.config"
                    "mscorlib.dll"
                    "system.dll"
                    "system.core.dll"
                    "system.numerics.dll"
                    "system.web.dll"
                    "tests.dll"
                    "tests.xml"
                    "tests.exe"
                ]
                |> List.forall (fun n -> not (x.ToLower().EndsWith n)))
            |> Seq.map (fun file ->
                let ppf = global.NuGet.PhysicalPackageFile()
                ppf.SourcePath <- file
                ppf.TargetPath <- "tools" +/ c.FrameworkVersion.GetNuGetLiteral() +/ Path.GetFileName(file)
                ppf)
            |> Seq.distinctBy (fun file -> file.TargetPath)
            |> Seq.iter builder.Files.Add
        builder.Files.Add
            (
                let ppf = global.NuGet.PhysicalPackageFile()
                ppf.SourcePath <- RootDir +/ ".build" +/ "WebSharper.targets"
                ppf.TargetPath <- "content" +/ "WebSharper.targets"
                ppf
            )
        builder.Save(out)
        F.Binary.FromBytes (out.ToArray())
        |> F.BinaryContent
    content.WriteFile(NuGetPackageFile)
    tracefn "Written %s" NuGetPackageFile

module Templates =

    let VsixFileName = sprintf "%s-%O.vsix" Config.PackageId Config.NuGetVersion
    let VsixFile = DotBuildDir +/ VsixFileName

    let Category = [sprintf "%s-%O" Config.PackageId Config.AssemblyVersion]
    let Guid = Guid("371cf828-9e17-41cb-b014-496f3e9e7171")

    let Identity =
        VP.Identity.Create (sprintf "%s-%O" Config.PackageId Config.AssemblyVersion) Guid

    let NuGetPackages =
        lazy
        VST.NuGetPackages.Create Identity [
            NG.Package.FromFile NuGetPackageFile
        ]

    let Library =
        lazy
        let dir = RootDir +/ "templates" +/ "library"
        let meta =
            let m =
                VST.TemplateData.Create
                    VST.FSharp
                    "Library"
                    "An F# library capable of containing JavaScript-compiled code"
                    Config.Icon
            m.DefaultName <- Some "Library"
            m
        let main =
            let i = VST.ProjectItem.FromTextFile (dir +/ "Main.fs")
            i.ReplaceParameters <- true
            i
        let project =
            let p =
                VST.Project.FromFile (dir +/ "Library.fsproj") [
                    VST.NestedProjectItem main
                ]
            p.ReplaceParameters <- true
            p
        let projectTemplate =
            let t = VST.ProjectTemplate.Create meta project
            t.NuGetPackages <- Some NuGetPackages.Value
            t
        projectTemplate

    let Extension =
        lazy
        let dir = RootDir +/ "templates" +/ "extension"
        let meta =
            let m =
                VST.TemplateData.Create
                    VST.FSharp
                    "Extension"
                    "Creates a new WebSharper binding to existing JavaScript code using \
                        the WebSharper Interface Generator tool"
                    Config.Icon
            m.DefaultName <- Some "Extension"
            m
        let main =
            let i = VST.ProjectItem.FromTextFile (dir +/ "Main.fs")
            i.ReplaceParameters <- true
            i
        let project =
            let p =
                VST.Project.FromFile (dir +/ "Extension.fsproj") [
                    VST.NestedProjectItem main
                ]
            p.ReplaceParameters <- true
            p
        let projectTemplate =
            let t = VST.ProjectTemplate.Create meta project
            t.NuGetPackages <- Some NuGetPackages.Value
            t
        projectTemplate

    let SiteletsWebsite =
        lazy
        let dir = RootDir +/ "templates" +/ "sitelets-website"
        let meta =
            let m =
                VST.TemplateData.Create
                    VST.FSharp
                    "Sitelet Website Definition"
                    "A WebSharper library with scaffolding to define a website using \
                     WebSharper sitelets. The website can be hosted inside a web server or \
                     generated to produce static HTML, CSS and JavaScript"
                    Config.Icon
            m.DefaultName <- Some "Website"
            m
        let main =
            let i = VST.ProjectItem.FromTextFile (dir +/ "Main.fs")
            i.ReplaceParameters <- true
            i
        let project =
            let p =
                VST.Project.FromFile (dir +/ "Website.fsproj") [
                    VST.NestedProjectItem main
                ]
            p.ReplaceParameters <- true
            p
        let projectTemplate =
            let t = VST.ProjectTemplate.Create meta project
            t.NuGetPackages <- Some NuGetPackages.Value
            t
        projectTemplate

    let SiteletsHtml =
        lazy
        let dir = RootDir +/ "templates" +/ "sitelets-html"
        let meta =
            let m =
                VST.TemplateData.Create
                    VST.FSharp
                    "Sitelet Html Generator"
                    "Generates static HTML, CSS, and JavaScript from a sitelet website definition."
                    Config.Icon
            m.DefaultName <- Some "HtmlSite"
            m
        let file repl name =
            let i = VST.ProjectItem.FromTextFile (dir +/ name)
            i.ReplaceParameters <- repl
            VST.NestedProjectItem i
        let project =
            let p =
                VST.Project.FromFile (dir +/ "HtmlSite.fsproj") [
                    file true "Main.fs"
                    file false "extra.files"
                    file false "Main.html"
                ]
            p.ReplaceParameters <- true
            p
        let projectTemplate =
            let t = VST.ProjectTemplate.Create meta project
            t.NuGetPackages <- Some NuGetPackages.Value
            t
        projectTemplate

    let SiteletsHost =
        lazy
        let dir = RootDir +/ "templates" +/ "sitelets-host"
        let meta =
            let m =
                VST.TemplateData.Create
                    VST.FSharp
                    "Sitelet Host Website"
                    "A C#-based web project for hosting WebSharper sitelets in a web server."
                    Config.Icon
            m.DefaultName <- Some "Web"
            m
        let file name =
            let i = VST.ProjectItem.FromTextFile (dir +/ name)
            i.ReplaceParameters <- true
            VST.NestedProjectItem i

        let folder name xs =
            let f = VST.Folder.Create name xs
            VST.NestedFolder f
        let project =
            let p =
                VST.Project.FromFile (dir +/ "Web.csproj") [
                    folder "Properties" [
                        file "AssemblyInfo.cs"
                    ]
                    file "Main.html"
                    file "Web.config"
                ]
            p.ReplaceParameters <- true
            p
        let projectTemplate =
            let t = VST.ProjectTemplate.Create meta project
            t.NuGetPackages <- Some NuGetPackages.Value
            t
        projectTemplate

    let WebSharperExtension =
        lazy
        let id =
            let id =
                VX.Identifier.Create
                    Config.Company
                    Identity
                    Config.PackageId
                    Config.Description
            id.Version <- Config.FileVersion
            id.Products <-
                [
                    VX.VSProduct.Create "10.0" [VX.Premium; VX.Pro; VX.Ultimate]
                    |> VX.VS
                    VX.VSProduct.Create "11.0" [VX.Premium; VX.Pro; VX.Ultimate]
                    |> VX.VS
                ]
            id
        let vsix =
            let proj (x: Lazy<_>) =
                VX.VsixContent.ProjectTemplate Category x.Value
            VX.Vsix.Create id [
                proj Library
                proj Extension
                proj SiteletsWebsite
                proj SiteletsHost
                proj SiteletsHtml
            ]
        VX.VsixFile.Create VsixFileName vsix

    let BuildExtension =
        T "BuildExtension" <| fun () ->
            WebSharperExtension.Value.WriteToDirectory DotBuildDir

let BuildConfigFile =
    T "BuildConfigFile" <| fun () ->
        let content =
            use w = new StringWriter()
            w.WriteLine("module Website.Config")
            let var (name: string) (value: string) : unit =
                fprintfn w @"let %s = @""%s""" name (value.Replace(@"""", @""""""))
            IntelliFactory.Build.Mercurial.InferTag RootDir
            |> Option.iter (var "Tag")
            var "PackageId" Config.PackageId
            var "Version" (string Config.NuGetVersion)
            var "AssemblyVersion" (string Config.AssemblyVersion)
            var "AssemblyFileVersion" (string Config.FileVersion)
            var "Description" Config.Description
            var "Website" Config.Website
            w.ToString()
            |> IntelliFactory.Build.FileSystem.TextContent
        content.WriteFile(DotBuildDir +/ "Config.fs")

let ZipPackageFile =
    RootDir +/ "Web" +/ "downloads" +/ sprintf "%s-%O.zip" Config.PackageId Config.NuGetVersion

let BuildZipPackage =
    T "BuildZipPackage" <| fun () ->
        ensureDirectory (Path.GetDirectoryName ZipPackageFile)
        let zip = new ZipFile()
        let addFile path =
            zip.AddEntry(Path.GetFileName path, File.ReadAllBytes path)
            |> ignore
        addFile NuGetPackageFile
        addFile (RootDir +/ "LICENSE.txt")
        addFile Templates.VsixFile
        zip.Save ZipPackageFile

CompressJavaScript
    ==> BuildConfigFile
    ==> BuildCompiler
    ==> BuildMain
    ==> BuildWebSharperTargets
    ==> BuildWebConfigTransform
    ==> BuildNuGet
    ==> Templates.BuildExtension
    ==> BuildZipPackage
    ==> BuildSite
    ==> Build

CleanCompressedJavaScript
    ==> CleanSite
    ==> CleanMain
    ==> CleanCompiler
    ==> Clean

RunTargetOrDefault Build

#endif