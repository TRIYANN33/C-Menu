Imports System
Imports System.Data
Imports System.Diagnostics
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.Data.Sqlite

Public Class FrmMenuAutonome
    Inherits Form

    Private ReadOnly menuBar As New MenuStrip()
    Private ReadOnly titleLabel As New Label()
    Private ReadOnly clock As New Timer With {.Interval = 1000}
    Private ReadOnly databasePath As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "C-Menu", "CMenu.db")

    Public Sub New()
        Text = "Menu autonome"
        StartPosition = FormStartPosition.CenterScreen
        Size = New Size(1050, 680)
        MinimumSize = New Size(650, 400)
        Font = New Font("Segoe UI", 10)
        BackColor = Color.FromArgb(241, 247, 237)

        titleLabel.Dock = DockStyle.Top
        titleLabel.Height = 65
        titleLabel.Padding = New Padding(15, 0, 15, 0)
        titleLabel.TextAlign = ContentAlignment.MiddleLeft
        titleLabel.Font = New Font("Segoe UI", 13, FontStyle.Bold)
        titleLabel.BackColor = Color.FromArgb(225, 244, 216)

        menuBar.Dock = DockStyle.Top
        menuBar.AutoSize = True
        menuBar.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow
        menuBar.BackColor = Color.FromArgb(226, 234, 240)
        menuBar.Padding = New Padding(10, 6, 10, 6)
        Controls.Add(menuBar)
        Controls.Add(titleLabel)
        MainMenuStrip = menuBar

        AddHandler clock.Tick, Sub(sender, e) RefreshClock()
        AddHandler Shown, Sub(sender, e)
            RefreshClock()
            LoadFromDatabase()
            clock.Start()
        End Sub
        AddHandler FormClosed, Sub(sender, e) clock.Stop()
    End Sub

    Private Sub RefreshClock()
        Dim nowValue = DateTime.Now
        titleLabel.Text = "Menus — Nous sommes le " &
            nowValue.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR")) &
            " à " & nowValue.ToString("HH:mm:ss")
    End Sub

    Private Sub LoadFromDatabase()
        menuBar.Items.Clear()
        If Not File.Exists(databasePath) Then
            ShowProblem("Base SQLite introuvable : " & databasePath &
                        Environment.NewLine & "Démarrez d'abord C-Menu pour créer la base.")
            Return
        End If
        Try
            Dim records As New List(Of MenuRow)()
            Dim connectionString = New SqliteConnectionStringBuilder With {
                .DataSource = databasePath, .Mode = SqliteOpenMode.ReadOnly
            }.ToString()
            Using cn As New SqliteConnection(connectionString)
                cn.Open()
                Using cmd = cn.CreateCommand()
                    cmd.CommandText =
                        "SELECT Frm_Menu, NumMenu, NumSousMenu, Menu, SousMenu, Chemin " &
                        "FROM Menus WHERE TRIM(COALESCE(Menu,'')) <> '' " &
                        "AND LOWER(TRIM(Menu)) NOT LIKE 'couleurs%' " &
                        "AND LOWER(TRIM(COALESCE(SousMenu,''))) NOT LIKE 'couleurs%' " &
                        "ORDER BY Frm_Menu COLLATE NOCASE, CAST(NumMenu AS INTEGER), " &
                        "CAST(NumSousMenu AS INTEGER), Menu COLLATE NOCASE, SousMenu COLLATE NOCASE"
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            records.Add(New MenuRow With {
                                .FormName = Value(reader, 0),
                                .MenuNumber = Value(reader, 1),
                                .SubNumber = Value(reader, 2),
                                .MenuText = Value(reader, 3),
                                .SubText = Value(reader, 4),
                                .Path = Value(reader, 5)
                            })
                        End While
                    End Using
                End Using
            End Using

            For Each group In records.GroupBy(
                Function(x) x.FormName & ChrW(31) & x.MenuNumber & ChrW(31) & x.MenuText,
                StringComparer.OrdinalIgnoreCase)
                Dim first = group.First()
                Dim top As New ToolStripMenuItem(first.MenuText.Trim()) With {
                    .AutoSize = True,
                    .ToolTipText = first.FormName
                }
                For Each record In group
                    If String.IsNullOrWhiteSpace(record.SubText) Then
                        If Not String.IsNullOrWhiteSpace(record.Path) Then
                            Dim destination = record.Path
                            AddHandler top.Click, Sub(sender, e) OpenPath(destination)
                        End If
                    Else
                        Dim child As New ToolStripMenuItem(record.SubText.Trim()) With {
                            .Tag = record.Path
                        }
                        AddHandler child.Click, Sub(sender, e)
                            OpenPath(CStr(DirectCast(sender, ToolStripMenuItem).Tag))
                        End Sub
                        top.DropDownItems.Add(child)
                    End If
                Next
                menuBar.Items.Add(top)
            Next
            If menuBar.Items.Count = 0 Then
                ShowProblem("Aucun menu disponible (les menus Couleurs sont exclus).")
            End If
        Catch ex As Exception
            ShowProblem("Lecture de SQLite impossible : " & ex.Message)
        End Try
    End Sub

    Private Shared Function Value(reader As SqliteDataReader, index As Integer) As String
        Return If(reader.IsDBNull(index), "", Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture))
    End Function

    Private Sub ShowProblem(message As String)
        menuBar.Items.Add(New ToolStripLabel("Aucun menu"))
        MessageBox.Show(Me, message, "Menu autonome", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub OpenPath(pathValue As String)
        If String.IsNullOrWhiteSpace(pathValue) Then
            MessageBox.Show(Me, "Aucun chemin défini pour ce sous-menu.", "Menu autonome")
            Return
        End If
        Try
            Dim destination = Environment.ExpandEnvironmentVariables(pathValue.Trim())
            Process.Start(New ProcessStartInfo(destination) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show(Me, "Impossible d'ouvrir : " & pathValue &
                            Environment.NewLine & ex.Message, "Menu autonome",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Class MenuRow
        Public Property FormName As String = ""
        Public Property MenuNumber As String = ""
        Public Property SubNumber As String = ""
        Public Property MenuText As String = ""
        Public Property SubText As String = ""
        Public Property Path As String = ""
    End Class
End Class
