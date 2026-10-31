Imports Microsoft.Data.Sqlite
Imports System.IO
Imports System.Collections.Generic
Imports System.Linq

Public Class MenuRepository
    Private ReadOnly connectionString As String

    Public Sub New()
#If DISTRIBUTED Then
        Dim folder = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "Menu")
        Dim databasePath = Path.Combine(folder, "C_Menu.db")
#Else
        Dim folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "C-Menu")
        Dim databasePath = Path.Combine(folder, "CMenu.db")
#End If
        Directory.CreateDirectory(folder)
        connectionString = New SqliteConnectionStringBuilder With {.DataSource = databasePath}.ToString()
        Using cn As New SqliteConnection(connectionString)
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS Menus (Id INTEGER PRIMARY KEY AUTOINCREMENT, SaisieLe TEXT NOT NULL, ModifierLe TEXT, NumMenu TEXT NOT NULL, NumSousMenu TEXT NOT NULL, Menu TEXT NOT NULL, SousMenu TEXT NOT NULL, Info01 TEXT NOT NULL, Info02 TEXT NOT NULL, Action TEXT NOT NULL, Chemin TEXT NOT NULL)"
                cmd.ExecuteNonQuery()
                cmd.CommandText = "PRAGMA table_info(Menus)"
                Dim exists As Boolean = False
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        If reader.GetString(1) = "Frm_Menu" Then exists = True
                    End While
                End Using
                If Not exists Then
                    cmd.CommandText = "ALTER TABLE Menus ADD COLUMN Frm_Menu TEXT NOT NULL DEFAULT ''"
                    cmd.ExecuteNonQuery()
                End If
                cmd.CommandText = "PRAGMA table_info(Menus)"
                Dim hasApplication As Boolean = False
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        If String.Equals(reader.GetString(1), "Application", StringComparison.OrdinalIgnoreCase) Then hasApplication = True
                    End While
                End Using
                If Not hasApplication Then
                    cmd.CommandText = "ALTER TABLE Menus ADD COLUMN Application TEXT NOT NULL DEFAULT ''"
                    cmd.ExecuteNonQuery()
                End If
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_Menus_NumMenu ON Menus(NumMenu, NumSousMenu)"
                cmd.ExecuteNonQuery()
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_Menus_Menu ON Menus(Menu, SousMenu)"
                cmd.ExecuteNonQuery()
            End Using
            EnsureStarterRecords(cn)
        End Using
    End Sub

    Private Shared Sub EnsureStarterRecords(cn As SqliteConnection)
        ' Ne jamais renommer ni déplacer les menus saisis par l'utilisateur.
        ' Les exemples 99 sont ajoutés seulement s'ils sont absents.
        ' Ne conserver qu'une seule ligne Exemple / Started pour le sous-menu 99/0.
        ' Ne touche à aucun autre enregistrement ni aux menus personnels.
        Using deduplicate = cn.CreateCommand()
            deduplicate.CommandText = "DELETE FROM Menus WHERE Id IN (SELECT Id FROM Menus WHERE NumMenu='99' AND NumSousMenu='0' AND LOWER(TRIM(Menu))='exemple' AND LOWER(TRIM(SousMenu))='started' AND Id NOT IN (SELECT MIN(Id) FROM Menus WHERE NumMenu='99' AND NumSousMenu='0' AND LOWER(TRIM(Menu))='exemple' AND LOWER(TRIM(SousMenu))='started'))"
            deduplicate.ExecuteNonQuery()
        End Using
        For Each subName In {"Started", "Sauvegarde", "Restauration"}
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "INSERT INTO Menus (SaisieLe, ModifierLe, NumMenu, NumSousMenu, Frm_Menu, Menu, SousMenu, Info01, Info02, Application, Action, Chemin) SELECT $now, $now, '99', $num, '', 'Exemple', $sub, '', '', '', $action, '' WHERE NOT EXISTS (SELECT 1 FROM Menus WHERE LOWER(TRIM(Menu))='exemple' AND LOWER(TRIM(SousMenu))=LOWER($sub))"
                cmd.Parameters.AddWithValue("$now", DateTime.Now.ToString("o"))
                cmd.Parameters.AddWithValue("$num", If(subName = "Started", "1", If(subName = "Sauvegarde", "2", "3")))
                cmd.Parameters.AddWithValue("$sub", subName)
                cmd.Parameters.AddWithValue("$action", "SYSTEM_" & subName.ToUpperInvariant())
                cmd.ExecuteNonQuery()
            End Using
        Next
        ' Exemples prédéfinis 4 à 29, ajoutés uniquement lorsqu'ils sont absents.
        Dim examples As (Number As Integer, Label As String, App As String, Action As String, Path As String)() = {
            (4, "Excel", "EXCEL", "OUVR-EXCEL", "C:\Program Files\Microsoft Office\root\Office16\EXCEL.EXE"),
            (5, "WORD", "WORD", "OUVR-WORD", "C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE"),
            (6, "MSACCESS", "MSACCESS", "OUVR-MSACCESS", "C:\Program Files\Microsoft Office\root\Office16\MSACCESS.EXE"),
            (7, "DOSSIERS", "DOSSIER", "OUVR-DOSSIER", "C:\Program Files"),
            (8, "FICHIERS", "FICHIER", "OUVR-FICHIER", "IMPOT 2026-09-28--19-21.txt"),
            (9, "Textes", "TEXTE", "OUVR-TEXTE", "IMPOT 2026-09-28--19-21.txt"),
            (10, "PDF", "PDF", "OUVR-PDF", "IMPOT 2026-09-28--19-21.txt"),
            (11, "Vidéos", "VIDEO", "OUVR-VIDEO", "IMPOT 2026-09-28--19-21.txt"),
            (12, "Photos", "PHOTO", "DOSSIER", "C:\Menu\applications\Photos"),
            (13, "Images", "IMAGES", "SYSTEM_IMAGES", "C:\Menu\applications\Images"),
            (14, "Diaporama", "DIAPORAMA", "SYSTEM_DIAPORAMA", ""),
            (15, "DOSS-DIAPORAMA", "DOSS-DIAPO", "OUVR-DOSSIER", "C:\Menu\applications\Diaporama"),
            (16, "Design", "DESIGN", "SYSTEM_DESIGN", ""),
            (17, "Anniversaires", "ANNIVERSAIRES", "SYSTEM_ANNIVERSAIRES", ""),
            (18, "Rendez-vous", "RDV", "SYSTEM_RDV", ""),
            (19, "PC", "PC", "OUVR-PC", "Ce PC"),
            (20, "Corbeille", "CORBEILLE", "OUVR-CORBEILLE", "Corbeille"),
            (21, "Downloads", "Downloads", "OUVR-Downloads", "C:\Users\OCTOPUS-007\Downloads"),
            (22, "Documents", "Documents", "OUVR-Documents", "C:\Users\OCTOPUS-007\Documents"),
            (23, "Applications", "APPLICATION", "OUVR-APPLICATION", "C:\Program Files\Audacity 4\bin\Audacity4.exe"),
            (24, "Applications", "APPLICATION", "OUVR-APPLICATION", "C:\Program Files (x86)\Brother\BrLauncher\BrLauncher.exe"),
            (25, "Web", "WEB", "OUVR-WEB", "https://www.impots.gouv.fr/"),
            (26, "Disque", "DD", "OUVR-DD", "file:///G:\"),
            (27, "DuckDuckGo", "EXPLORATEUR", "OUVR-EXPLORATEUR", "DuckDuckGo.DesktopBrowser_ya2fgkz3nks94!App"),
            (28, "Firefox", "EXPLORATEUR", "OUVR-EXPLORATEUR", "C:\Program Files\Mozilla Firefox\firefox.exe"),
            (29, "Applications", "APPLICATION", "OUVR-APPLICATION", "C:\Program Files\Avast Software\Suite\AvastUI.exe")
        }
        For Each example In examples
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "INSERT INTO Menus (SaisieLe, ModifierLe, NumMenu, NumSousMenu, Frm_Menu, Menu, SousMenu, Info01, Info02, Application, Action, Chemin) SELECT $now, $now, '99', $num, '', 'Exemple', $label, $info1, $info2, $app, $action, $path WHERE NOT EXISTS (SELECT 1 FROM Menus WHERE NumMenu='99' AND NumSousMenu=$num AND LOWER(TRIM(Menu))='exemple')"
                cmd.Parameters.AddWithValue("$now", DateTime.Now.ToString("o"))
                cmd.Parameters.AddWithValue("$num", example.Number.ToString())
                cmd.Parameters.AddWithValue("$label", example.Label)
                cmd.Parameters.AddWithValue("$info1", If(example.Number = 25, "Mot de passe", ""))
                cmd.Parameters.AddWithValue("$info2", If(example.Number = 25, "Login", ""))
                cmd.Parameters.AddWithValue("$app", example.App)
                cmd.Parameters.AddWithValue("$action", example.Action)
                cmd.Parameters.AddWithValue("$path", example.Path)
                cmd.ExecuteNonQuery()
            End Using
        Next
    End Sub

    Private Shared Sub Bind(cmd As SqliteCommand, item As MenuItem)
        cmd.Parameters.AddWithValue("$numMenu", item.NumMenu)
        cmd.Parameters.AddWithValue("$numSousMenu", item.NumSousMenu)
        cmd.Parameters.AddWithValue("$frmMenu", item.Frm_Menu)
        cmd.Parameters.AddWithValue("$menu", item.Menu)
        cmd.Parameters.AddWithValue("$sousMenu", item.SousMenu)
        cmd.Parameters.AddWithValue("$info01", item.Info01)
        cmd.Parameters.AddWithValue("$info02", item.Info02)
        cmd.Parameters.AddWithValue("$application", item.Application)
        cmd.Parameters.AddWithValue("$action", item.Action)
        cmd.Parameters.AddWithValue("$chemin", item.Chemin)
    End Sub

    Public Sub Insert(item As MenuItem)
        Using cn As New SqliteConnection(connectionString)
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "INSERT INTO Menus (SaisieLe, ModifierLe, NumMenu, NumSousMenu, Frm_Menu, Menu, SousMenu, Info01, Info02, Application, Action, Chemin) VALUES ($created, $modified, $numMenu, $numSousMenu, $frmMenu, $menu, $sousMenu, $info01, $info02, $application, $action, $chemin)"
                Dim nowStamp = DateTime.Now.ToString("o")
                cmd.Parameters.AddWithValue("$created", nowStamp)
                cmd.Parameters.AddWithValue("$modified", nowStamp)
                Bind(cmd, item)
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    Public Sub Update(item As MenuItem)
        Using cn As New SqliteConnection(connectionString)
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "UPDATE Menus SET ModifierLe=$modified, NumMenu=$numMenu, NumSousMenu=$numSousMenu, Frm_Menu=$frmMenu, Menu=$menu, SousMenu=$sousMenu, Info01=$info01, Info02=$info02, Application=$application, Action=$action, Chemin=$chemin WHERE Id=$id AND NOT (LOWER(TRIM(Menu))='exemple' AND NumMenu='99')"
                cmd.Parameters.AddWithValue("$modified", DateTime.Now.ToString("o"))
                cmd.Parameters.AddWithValue("$id", item.Id)
                Bind(cmd, item)
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    Public Sub Delete(id As Integer)
        Using cn As New SqliteConnection(connectionString)
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "DELETE FROM Menus WHERE Id=$id AND NOT (LOWER(TRIM(Menu))='exemple' AND NumMenu='99') AND NOT (LOWER(REPLACE(REPLACE(COALESCE(Menu,''),'-',''),' ','')) LIKE '%starterapplis%' OR LOWER(REPLACE(REPLACE(COALESCE(SousMenu,''),'-',''),' ','')) LIKE '%starterapplis%' OR LOWER(REPLACE(REPLACE(COALESCE(Action,''),'-',''),' ','')) LIKE '%starterapplis%')"
                cmd.Parameters.AddWithValue("$id", id)
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    Private Shared Function EscapeLike(value As String) As String
        Return value.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_")
    End Function

    Public Function GetAll(Optional search As String = "", Optional sortFields As List(Of (Field As String, Descending As Boolean)) = Nothing, Optional filters As List(Of (Field As String, OperatorName As String, Value As String)) = Nothing) As List(Of MenuItem)
        Dim results As New List(Of MenuItem)()
        Using cn As New SqliteConnection(connectionString)
            cn.Open()
            Using cmd = cn.CreateCommand()
                Dim allowed As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                    "Id", "NumMenu", "NumSousMenu", "Frm_Menu", "Menu", "SousMenu",
                    "Info01", "Info02", "Application", "Action", "Chemin", "SaisieLe", "ModifierLe"
                }
                Dim orderParts As New List(Of String)()
                If sortFields IsNot Nothing Then
                    For Each spec In sortFields.Take(3)
                        If Not allowed.Contains(spec.Field) Then Throw New ArgumentException("Champ de tri invalide.")
                        Dim col = """" & spec.Field.Replace("""", """""") & """"
                        Dim collate = If(spec.Field = "Id" OrElse spec.Field = "SaisieLe" OrElse spec.Field = "ModifierLe", "", " COLLATE NOCASE")
                        If spec.Field = "NumMenu" OrElse spec.Field = "NumSousMenu" Then
                            col = "CAST(" & col & " AS INTEGER)"
                            collate = ""
                        End If
                        orderParts.Add(col & collate & If(spec.Descending, " DESC", " ASC"))
                    Next
                End If
                orderParts.Add("Id DESC")
                Dim conditions As New List(Of String) From {
                    "($search='' OR NumMenu LIKE $pattern OR NumSousMenu LIKE $pattern OR Frm_Menu LIKE $pattern OR Menu LIKE $pattern OR SousMenu LIKE $pattern OR Application LIKE $pattern)"
                }
                If filters IsNot Nothing Then
                    Dim index As Integer = 0
                    For Each spec In filters.Take(3)
                        If Not allowed.Contains(spec.Field) Then Throw New ArgumentException("Champ de filtre invalide.")
                        Dim columnName = """" & spec.Field.Replace("""", """""") & """"
                        Dim paramName = "$filter" & index.ToString()
                        Dim pattern As String = spec.Value
                        Select Case spec.OperatorName
                            Case "contains"
                                conditions.Add(columnName & " LIKE " & paramName & " ESCAPE '~'")
                                pattern = "%" & EscapeLike(pattern) & "%"
                            Case "starts"
                                conditions.Add(columnName & " LIKE " & paramName & " ESCAPE '~'")
                                pattern = EscapeLike(pattern) & "%"
                            Case "ends"
                                conditions.Add(columnName & " LIKE " & paramName & " ESCAPE '~'")
                                pattern = "%" & EscapeLike(pattern)
                            Case "equals"
                                conditions.Add(columnName & " = " & paramName & " COLLATE NOCASE")
                            Case "not_equals"
                                conditions.Add(columnName & " <> " & paramName & " COLLATE NOCASE")
                            Case Else
                                Throw New ArgumentException("Condition de filtre invalide.")
                        End Select
                        cmd.Parameters.AddWithValue(paramName, pattern)
                        index += 1
                    Next
                End If
                cmd.CommandText = "SELECT Id,SaisieLe,ModifierLe,NumMenu,NumSousMenu,Frm_Menu,Menu,SousMenu,Info01,Info02,Application,Action,Chemin FROM Menus WHERE " & String.Join(" AND ", conditions) & " ORDER BY " & String.Join(", ", orderParts)
                cmd.Parameters.AddWithValue("$search", search)
                cmd.Parameters.AddWithValue("$pattern", "%" & search & "%")
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        Dim modified As DateTime? = Nothing
                        If Not reader.IsDBNull(2) Then modified = DateTime.Parse(reader.GetString(2), Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.RoundtripKind)
                        results.Add(New MenuItem With {
                            .Id = reader.GetInt32(0),
                            .SaisieLe = DateTime.Parse(reader.GetString(1), Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.RoundtripKind),
                            .ModifierLe = modified,
                            .NumMenu = reader.GetString(3), .NumSousMenu = reader.GetString(4),
                            .Frm_Menu = reader.GetString(5), .Menu = reader.GetString(6), .SousMenu = reader.GetString(7),
                            .Info01 = reader.GetString(8), .Info02 = reader.GetString(9),
                            .Application = reader.GetString(10), .Action = reader.GetString(11), .Chemin = reader.GetString(12)})
                    End While
                End Using
            End Using
        End Using
        Return results
    End Function
End Class
