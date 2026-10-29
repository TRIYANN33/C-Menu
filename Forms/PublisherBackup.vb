Imports System
Imports System.IO
Imports Microsoft.Data.Sqlite

Public NotInheritable Class PublisherBackup
    Private Sub New()
    End Sub

    Public Shared ReadOnly Property DatabasePath As String
        Get
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "C-Menu", "CMenu.db")
        End Get
    End Property

    Public Shared Sub SaveTo(destination As String)
        If Not File.Exists(DatabasePath) Then Throw New FileNotFoundException("Base de MenuPublier introuvable.", DatabasePath)
        If Path.GetFullPath(destination).Equals(Path.GetFullPath(DatabasePath), StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("Choisissez un fichier différent de la base utilisée.")
        End If
        Using source As New SqliteConnection(New SqliteConnectionStringBuilder With {
            .DataSource = DatabasePath, .Mode = SqliteOpenMode.ReadOnly
        }.ToString())
            Using target As New SqliteConnection(New SqliteConnectionStringBuilder With {
                .DataSource = destination
            }.ToString())
                source.Open()
                target.Open()
                source.BackupDatabase(target)
            End Using
        End Using
    End Sub

    Public Shared Sub RestoreFrom(sourcePath As String)
        If Not File.Exists(sourcePath) Then Throw New FileNotFoundException("Sauvegarde introuvable.", sourcePath)
        If Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(DatabasePath), StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("Le fichier sélectionné est la base actuelle.")
        End If
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath))
        Using source As New SqliteConnection(New SqliteConnectionStringBuilder With {
            .DataSource = sourcePath, .Mode = SqliteOpenMode.ReadOnly
        }.ToString())
            source.Open()
            Using check = source.CreateCommand()
                check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Menus'"
                If Convert.ToInt32(check.ExecuteScalar()) = 0 Then
                    Throw New InvalidDataException("La sauvegarde ne contient pas la table Menus.")
                End If
            End Using
            Using target As New SqliteConnection(New SqliteConnectionStringBuilder With {
                .DataSource = DatabasePath
            }.ToString())
                target.Open()
                source.BackupDatabase(target)
            End Using
        End Using
    End Sub
End Class
