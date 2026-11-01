Imports System.Drawing
Imports System.IO
Imports System.Text.Json

Public Class MenuColors
    Public Property TitleBar As Integer = Color.FromArgb(167, 232, 213).ToArgb()
    Public Property Labels As Integer = Color.Black.ToArgb()
    Public Property Toolbar As Integer = Color.FromArgb(226, 234, 240).ToArgb()
    Public Property Entry As Integer = Color.FromArgb(29, 171, 79).ToArgb()
    Public Property Table As Integer = Color.White.ToArgb()

    Private Shared ReadOnly ConfigPath As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "C-Menu", "colors.json")

    Public Shared Function Load() As MenuColors
        Try
            If File.Exists(ConfigPath) Then
                Dim saved = JsonSerializer.Deserialize(Of MenuColors)(File.ReadAllText(ConfigPath))
                If saved IsNot Nothing Then Return saved
            End If
        Catch
            ' En cas de configuration endommagée, conserver les couleurs par défaut.
        End Try
        Return New MenuColors()
    End Function

    Public Sub Save()
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath))
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Me))
    End Sub

    Public Function Copy() As MenuColors
        Return New MenuColors With {.TitleBar = TitleBar, .Labels = Labels, .Toolbar = Toolbar, .Entry = Entry, .Table = Table}
    End Function
End Class
