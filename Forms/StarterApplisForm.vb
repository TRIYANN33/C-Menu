Imports System
Imports System.Data
Imports System.IO
Imports System.Drawing
Imports System.Windows.Forms
Imports Microsoft.Data.Sqlite

Public Class StarterApplisForm
    Inherits Form

    Private ReadOnly dbPath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "C-Menu", "CMenu.db")
    Private ReadOnly grille As New DataGridView()
    Private ReadOnly actionNom As New TextBox()
    Private ReadOnly saisiele As New TextBox()
    Private ReadOnly modifierle As New TextBox()
    Private ReadOnly sauvegarde As New TextBox()
    Private ReadOnly libelle As New TextBox()
    Private ReadOnly applicationNom As New TextBox()
    Private ReadOnly chemin As New TextBox()
    Private selectedId As Long = 0

    Public Sub New()
        Text = "Starter-Applis — Gestion des applications"
        Size = New Size(1050, 650)
        MinimumSize = New Size(750, 500)
        StartPosition = FormStartPosition.CenterParent
        Font = New Font("Segoe UI", 10)
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 48, .Padding = New Padding(8)}
        AddButton(buttons, "Nouveau", AddressOf Nouveau)
        AddButton(buttons, "Ajouter", AddressOf Ajouter)
        AddButton(buttons, "Modifier", AddressOf Modifier)
        AddButton(buttons, "Supprimer", AddressOf Supprimer)
        AddButton(buttons, "Sauvegarder", AddressOf Sauvegarder)
        AddButton(buttons, "Parcourir...", AddressOf Parcourir)
        AddButton(buttons, "Démarrer dans l’ordre", AddressOf Demarrer)
        Dim editor As New TableLayoutPanel With {.Dock = DockStyle.Top, .Height = 220, .ColumnCount = 2, .RowCount = 7, .Padding = New Padding(12)}
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 135))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        AddField(editor, "Saisiele", saisiele, 0, True)
        AddField(editor, "Modifierle", modifierle, 1, True)
        AddField(editor, "Sauvegarde", sauvegarde, 2, True)
        AddField(editor, "Libellé", libelle, 3, False)
        AddField(editor, "Application", applicationNom, 4, False)
        AddField(editor, "ACTION", actionNom, 5, False)
        AddField(editor, "Lien", chemin, 6, False)
        grille.Dock = DockStyle.Fill
        grille.ReadOnly = True
        grille.AllowUserToAddRows = False
        grille.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grille.MultiSelect = False
        grille.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        AddHandler grille.SelectionChanged, AddressOf SelectionChanged
        Controls.Add(grille)
        Controls.Add(editor)
        Controls.Add(buttons)
        Try
            InitDb()
            Charger()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Starter-Applis", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub AddButton(panel As FlowLayoutPanel, title As String, handler As EventHandler)
        Dim b As New Button With {.Text = title, .AutoSize = True}
        AddHandler b.Click, handler
        panel.Controls.Add(b)
    End Sub

    Private Sub AddField(panel As TableLayoutPanel, title As String, tb As TextBox, row As Integer, ro As Boolean)
        panel.Controls.Add(New Label With {.Text = title, .Dock = DockStyle.Fill}, 0, row)
        tb.Dock = DockStyle.Fill
        tb.ReadOnly = ro
        panel.Controls.Add(tb, 1, row)
    End Sub

    Private Function Connexion() As SqliteConnection
        Return New SqliteConnection("Data Source=" & dbPath)
    End Function

    Private Sub BackupStarterData()
        Dim backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "C-Menu-Sauvegardes")
        Directory.CreateDirectory(backupFolder)
        Dim target = Path.Combine(backupFolder, "StarterApplis-" & DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") & ".db")
        Using source = Connexion()
            source.Open()
            Using backup As New SqliteConnection("Data Source=" & target)
                backup.Open()
                source.BackupDatabase(backup)
            End Using
        End Using
    End Sub

    Private Sub InitDb()
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath))
        Using cn = Connexion()
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS StarterApplis (Id INTEGER PRIMARY KEY AUTOINCREMENT, Saisiele TEXT NOT NULL, Modifierle TEXT, Sauvegarde TEXT, Libelle TEXT NOT NULL, Application TEXT NOT NULL, Chemin TEXT NOT NULL)"
                cmd.ExecuteNonQuery()
                cmd.CommandText = "PRAGMA table_info(StarterApplis)"
                Dim hasAction As Boolean = False
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        If String.Equals(reader.GetString(1), "Action", StringComparison.OrdinalIgnoreCase) Then hasAction = True
                    End While
                End Using
                If Not hasAction Then
                    cmd.CommandText = "ALTER TABLE StarterApplis ADD COLUMN Action TEXT NOT NULL DEFAULT ''"
                    cmd.ExecuteNonQuery()
                End If
            End Using
        End Using
    End Sub

    Private Sub Charger()
        Using cn = Connexion()
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "SELECT Id, Saisiele, Modifierle, Sauvegarde, Libelle AS [Libellé], Application, Action, Chemin AS Lien FROM StarterApplis ORDER BY Id"
                Using reader = cmd.ExecuteReader()
                    Dim table As New DataTable()
                    table.Load(reader)
                    grille.DataSource = table
                End Using
            End Using
        End Using
        If grille.Columns.Contains("Id") Then grille.Columns("Id").Visible = False
    End Sub

    Private Sub SelectionChanged(sender As Object, e As EventArgs)
        If grille.CurrentRow Is Nothing OrElse grille.CurrentRow.DataBoundItem Is Nothing Then Return
        Dim row = DirectCast(grille.CurrentRow.DataBoundItem, DataRowView)
        selectedId = CLng(row("Id"))
        saisiele.Text = Convert.ToString(row("Saisiele"))
        modifierle.Text = Convert.ToString(row("Modifierle"))
        sauvegarde.Text = Convert.ToString(row("Sauvegarde"))
        libelle.Text = Convert.ToString(row("Libellé"))
        applicationNom.Text = Convert.ToString(row("Application"))
        chemin.Text = Convert.ToString(row("Lien"))
        actionNom.Text = Convert.ToString(row("Action"))
    End Sub

    Private Sub Nouveau(sender As Object, e As EventArgs)
        grille.ClearSelection()
        grille.CurrentCell = Nothing
        selectedId = 0
        ' Prévisualisation de la date de saisie ; l'heure définitive est fixée à l'enregistrement.
        saisiele.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
        modifierle.Text = ""
        sauvegarde.Text = ""
        libelle.Text = ""
        applicationNom.Text = ""
        chemin.Text = ""
        actionNom.Text = ""
        libelle.Focus()
    End Sub

    Private Function Valide() As Boolean
        If String.IsNullOrWhiteSpace(libelle.Text) OrElse String.IsNullOrWhiteSpace(applicationNom.Text) OrElse String.IsNullOrWhiteSpace(chemin.Text) Then
            MessageBox.Show(Me, "Libellé, Application et Chemin sont obligatoires.")
            Return False
        End If
        Return True
    End Function

    Private Sub Ajouter(sender As Object, e As EventArgs)
        If Not Valide() Then Return
        Try
            BackupStarterData()
            Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            Using cn = Connexion()
                cn.Open()
                Using cmd = cn.CreateCommand()
                    cmd.CommandText = "INSERT INTO StarterApplis (Saisiele, Modifierle, Sauvegarde, Libelle, Application, Action, Chemin) VALUES ($s, '', $b, $l, $a, $act, $c)"
                    cmd.Parameters.AddWithValue("$s", nowText)
                    cmd.Parameters.AddWithValue("$b", nowText)
                    cmd.Parameters.AddWithValue("$l", libelle.Text.Trim())
                    cmd.Parameters.AddWithValue("$a", applicationNom.Text.Trim())
                    cmd.Parameters.AddWithValue("$act", actionNom.Text.Trim())
                    cmd.Parameters.AddWithValue("$c", chemin.Text.Trim())
                    cmd.ExecuteNonQuery()
                End Using
            End Using
            Charger()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message)
        End Try
    End Sub

    Private Sub Modifier(sender As Object, e As EventArgs)
        If selectedId = 0 OrElse Not Valide() Then Return
        Try
            BackupStarterData()
            Using cn = Connexion()
                cn.Open()
                Using cmd = cn.CreateCommand()
                    cmd.CommandText = "UPDATE StarterApplis SET Modifierle=$m, Sauvegarde=$m, Libelle=$l, Application=$a, Action=$act, Chemin=$c WHERE Id=$id"
                    cmd.Parameters.AddWithValue("$m", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                    cmd.Parameters.AddWithValue("$l", libelle.Text.Trim())
                    cmd.Parameters.AddWithValue("$a", applicationNom.Text.Trim())
                    cmd.Parameters.AddWithValue("$act", actionNom.Text.Trim())
                    cmd.Parameters.AddWithValue("$c", chemin.Text.Trim())
                    cmd.Parameters.AddWithValue("$id", selectedId)
                    cmd.ExecuteNonQuery()
                End Using
            End Using
            Charger()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message)
        End Try
    End Sub

    Private Sub Demarrer(sender As Object, e As EventArgs)
        Using cn = Connexion()
            cn.Open()
            Using cmd = cn.CreateCommand()
                cmd.CommandText = "SELECT Application, Chemin FROM StarterApplis ORDER BY Id"
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        Dim target = reader.GetString(1)
                        Try
                            Process.Start(New ProcessStartInfo(target) With {.UseShellExecute = True})
                        Catch ex As Exception
                            MessageBox.Show(Me, "Ouverture impossible : " & reader.GetString(0) & Environment.NewLine & ex.Message)
                        End Try
                    End While
                End Using
            End Using
        End Using
    End Sub

    Private Sub Sauvegarder(sender As Object, e As EventArgs)
        If selectedId = 0 Then
            Ajouter(sender, e)
        Else
            Modifier(sender, e)
        End If
    End Sub

    Private Sub Supprimer(sender As Object, e As EventArgs)
        If selectedId = 0 Then Return
        If String.Equals(libelle.Text.Trim(), "Starter-Applis", StringComparison.OrdinalIgnoreCase) OrElse String.Equals(applicationNom.Text.Trim(), "Starter-Applis", StringComparison.OrdinalIgnoreCase) Then
            MessageBox.Show(Me, "Starter-Applis est protégé contre la suppression.")
            Return
        End If
        If MessageBox.Show(Me, "Supprimer cet enregistrement ?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
        Try
            BackupStarterData()
            Using cn = Connexion()
                cn.Open()
                Using cmd = cn.CreateCommand()
                    cmd.CommandText = "DELETE FROM StarterApplis WHERE Id=$id"
                    cmd.Parameters.AddWithValue("$id", selectedId)
                    cmd.ExecuteNonQuery()
                End Using
            End Using
            selectedId = 0
            Charger()
            Nouveau(sender, e)
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message)
        End Try
    End Sub

    Private Sub Parcourir(sender As Object, e As EventArgs)
        Using dlg As New OpenFileDialog With {.Filter = "Applications (*.exe)|*.exe|Tous les fichiers (*.*)|*.*"}
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                chemin.Text = dlg.FileName
                applicationNom.Text = Path.GetFileName(dlg.FileName)
            End If
        End Using
    End Sub
End Class
