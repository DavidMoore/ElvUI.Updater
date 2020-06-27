using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Windows;

//In order to begin building localizable applications, set
//<UICulture>CultureYouAreCodingWith</UICulture> in your .csproj file
//inside a <PropertyGroup>.  For example, if you are using US english
//in your source files, set the <UICulture> to en-US.  Then uncomment
//the NeutralResourceLanguage attribute below.  Update the "en-US" in
//the line below to match the UICulture setting in the project file.

[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.Satellite)]


[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located
                                     //(used if a resource is not found in the page,
                                     // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located
                                              //(used if a resource is not found in the page,
                                              // app, or any theme specific resource dictionaries)
)]


[assembly: AssemblyTitle("ElvUI Updater")]
[assembly: AssemblyDescription("Utility for keeping your ElvUI WoW add-on up to date")]
[assembly: AssemblyCompany("Sad Robot")]
[assembly: AssemblyProduct("ElvUI Updater")]
[assembly: AssemblyCopyright("Copyright © David Moore 2020")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("0.9.2.0")]
[assembly: AssemblyFileVersion("0.9.2")]
[assembly: AssemblyInformationalVersion("0.9.2")]

[assembly: AssemblyConfiguration("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: Guid("BB27318D-E84E-41F3-A6B4-DFD0C6F572B3")]