Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Threading.Tasks

Public NotInheritable Class CMenuPublisher
    Private Sub New()
    End Sub

    Public Shared Async Function PublishAsync() As Task(Of String)
        Dim current As DirectoryInfo = New DirectoryInfo(AppContext.BaseDirectory)
        Dim projectPath As String = Nothing
        While current IsNot Nothing
            Dim candidate = Path.Combine(current.FullName, "C-Menu.vbproj")
            If File.Exists(candidate) Then
                projectPath = candidate
                Exit While
            End If
            current = current.Parent
        End While
        If projectPath Is Nothing Then
            Throw New FileNotFoundException("Projet C-Menu.vbproj introuvable. Lancez cette commande depuis C-Menu ouvert dans Visual Studio, avec les sources disponibles.")
        End If

        Dim destination = Path.Combine("C:\", "Menu", "C-Menu")
        Dim temporary = Path.Combine(Path.GetTempPath(), "CMenu-Publish-" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(temporary)
        Try
            Dim start As New ProcessStartInfo("dotnet") With {
                .WorkingDirectory = Path.GetDirectoryName(projectPath),
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True
            }
            For Each arg In {"publish", projectPath, "-c", "Release", "-r", "win-x64",
                             "--self-contained", "true", "-p:PublishSingleFile=true",
                             "-p:IncludeNativeLibrariesForSelfExtract=true",
                             "-p:DebugType=None", "-p:DebugSymbols=false", "-o", temporary}
                start.ArgumentList.Add(arg)
            Next
            Using proc = Process.Start(start)
                If proc Is Nothing Then Throw New InvalidOperationException("Impossible de lancer dotnet.")
                Dim outputTask = proc.StandardOutput.ReadToEndAsync()
                Dim errorTask = proc.StandardError.ReadToEndAsync()
                Await proc.WaitForExitAsync()
                Dim output = Await outputTask
                Dim errors = Await errorTask
                If proc.ExitCode <> 0 Then Throw New InvalidOperationException("Échec de publication :" & vbCrLf & output & vbCrLf & errors)
            End Using
            Dim executable = Path.Combine(temporary, "C-Menu.exe")
            If Not File.Exists(executable) Then Throw New FileNotFoundException("C-Menu.exe absent après publication.", executable)
            Directory.CreateDirectory(destination)
            For Each filePath In Directory.GetFiles(temporary)
                Dim target = Path.Combine(destination, Path.GetFileName(filePath))
                File.Copy(filePath, target, True)
            Next
            Return Path.Combine(destination, "C-Menu.exe")
        Finally
            Try
                Directory.Delete(temporary, True)
            Catch
            End Try
        End Try
    End Function
End Class
