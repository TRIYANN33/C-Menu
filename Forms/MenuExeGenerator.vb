Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Text
Imports System.Threading.Tasks
Imports Microsoft.Data.Sqlite

Public NotInheritable Class MenuExeGenerator
    Private Sub New()
    End Sub

    Private Shared Function EmbeddedSource(name As String) As String
        Dim asm = GetType(MenuExeGenerator).Assembly
        Dim resourceName = "CMenu.Template." & name
        Using stream = asm.GetManifestResourceStream(resourceName)
            If stream Is Nothing Then Throw New FileNotFoundException("Ressource intégrée absente : " & resourceName)
            Using reader As New StreamReader(stream, Encoding.UTF8)
                Return reader.ReadToEnd()
            End Using
        End Using
    End Function

    Public Shared Async Function CreateAsync(formName As String) As Task(Of String)
        If String.IsNullOrWhiteSpace(formName) Then Throw New ArgumentException("Frm_Menu est vide.")
        Dim safe = New String(formName.Trim().Select(Function(c) If(Char.IsLetterOrDigit(c) OrElse c = "-"c OrElse c = "_"c, c, "_"c)).ToArray())
        If safe.Length = 0 Then Throw New ArgumentException("Nom de formulaire invalide.")
        If safe.Length > 60 Then safe = safe.Substring(0, 60)
        Dim outputRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "C-Menu", "Formulaires", safe)
        Dim sourceFolder = Path.Combine(outputRoot, "Sources")
        Dim publishFolder = Path.Combine("C:\\", "Menu", "applications", safe)
        Directory.CreateDirectory(sourceFolder)
        Directory.CreateDirectory(publishFolder)

        File.WriteAllText(Path.Combine(sourceFolder, "Program.vb"), EmbeddedSource("Program.vb"), Encoding.UTF8)
        File.WriteAllText(Path.Combine(sourceFolder, "FrmMenuAutonome.vb"), EmbeddedSource("FrmMenuAutonome.vb"), Encoding.UTF8)
        Dim config = "Public Module GeneratedConfig" & vbCrLf &
            "    Public Const FormName As String = " & QuoteVB(formName.Trim()) & vbCrLf &
            "    Public Const SafeName As String = " & QuoteVB(safe) & vbCrLf &
            "End Module" & vbCrLf
        File.WriteAllText(Path.Combine(sourceFolder, "GeneratedConfig.vb"), config, Encoding.UTF8)
        Dim project = "<Project Sdk=""Microsoft.NET.Sdk"">" & vbCrLf &
            "<PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net6.0-windows</TargetFramework>" &
            "<UseWindowsForms>true</UseWindowsForms><RootNamespace>MenuAutonome</RootNamespace>" &
            "<StartupObject>MenuAutonome.Program</StartupObject><MyType>Empty</MyType>" &
            "<AssemblyName>" & safe & "</AssemblyName></PropertyGroup>" &
            "<ItemGroup><PackageReference Include=""Microsoft.Data.Sqlite"" Version=""6.0.36"" /></ItemGroup></Project>"
        Dim projectPath = Path.Combine(sourceFolder, "Menu-Autonome.vbproj")
        File.WriteAllText(projectPath, project, Encoding.UTF8)

        Dim start As New ProcessStartInfo("dotnet") With {
            .WorkingDirectory = sourceFolder,
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True
        }
        For Each arg In {"publish", projectPath, "-c", "Release", "-r", "win-x64",
                         "--self-contained", "true", "-p:PublishSingleFile=true",
                         "-p:IncludeNativeLibrariesForSelfExtract=true",
                         "-o", publishFolder}
            start.ArgumentList.Add(arg)
        Next
        Using proc = Process.Start(start)
            If proc Is Nothing Then Throw New InvalidOperationException("Impossible de lancer dotnet.")
            Dim stdout = proc.StandardOutput.ReadToEndAsync()
            Dim stderr = proc.StandardError.ReadToEndAsync()
            Await proc.WaitForExitAsync()
            Dim output = Await stdout
            Dim errors = Await stderr
            If proc.ExitCode <> 0 Then Throw New InvalidOperationException("Publication impossible :" & vbCrLf & output & vbCrLf & errors)
        End Using
        Dim exePath = Path.Combine(publishFolder, safe & ".exe")
        If Not File.Exists(exePath) Then Throw New FileNotFoundException("L'exécutable n'a pas été créé.", exePath)
        Dim originalDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "C-Menu", "CMenu.db")
        If Not File.Exists(originalDb) Then Throw New FileNotFoundException("Base SQLite source introuvable.", originalDb)
        Dim targetDb = Path.Combine(publishFolder, "CMenu.db")
        Using source As New SqliteConnection(New SqliteConnectionStringBuilder With {
            .DataSource = originalDb, .Mode = SqliteOpenMode.ReadOnly
        }.ToString())
            Using target As New SqliteConnection(New SqliteConnectionStringBuilder With {
                .DataSource = targetDb
            }.ToString())
                source.Open()
                target.Open()
                source.BackupDatabase(target)
            End Using
        End Using
        Return exePath
    End Function

    Private Shared Function QuoteVB(value As String) As String
        Return """" & value.Replace("""", """""") & """"
    End Function
End Class
