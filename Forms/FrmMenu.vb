Imports System.Drawing
Imports System.Globalization
Imports System.Windows.Forms
Imports System.ComponentModel
Imports System.Collections.Generic
Imports System.Linq
Imports System.Diagnostics
Imports System.IO
Imports System.IO.Compression
Imports System.Text.Json

Public Class FrmMenu
    Inherits Form

    <System.Runtime.InteropServices.DllImport("dwmapi.dll")>
    Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, attribute As Integer,
                                                   ByRef value As Integer, size As Integer) As Integer
    End Function

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        If Environment.OSVersion.Version.Build >= 22000 Then
            ' DWMWA_CAPTION_COLOR (Windows 11) : vert d'eau #A7E8D5.
            Dim captionColor As Integer = &HD5E8A7
            DwmSetWindowAttribute(Handle, 35, captionColor, 4)
            ' DWMWA_TEXT_COLOR : noir.
            Dim textColor As Integer = 0
            DwmSetWindowAttribute(Handle, 36, textColor, 4)
        End If
    End Sub


    Private ReadOnly titleClock As New Timer With {.Interval = 1000}
    Private ReadOnly repo As New MenuRepository()
    Private themeColors As MenuColors = MenuColors.Load()
    Private ReadOnly fieldLabels As New List(Of Label)()
    Private toolbarPanel As FlowLayoutPanel
    Private editorPanel As TableLayoutPanel
    Private ReadOnly fields As New Dictionary(Of String, Control)()
    Private ReadOnly comboKeys As New HashSet(Of String) From {"Frm_Menu", "Menu", "SousMenu", "Info01", "Info02", "Application", "Action"}
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly idBox As New TextBox()
    Private ReadOnly dateCreated As New TextBox()
    Private ReadOnly dateModified As New TextBox()
    Private ReadOnly searchBox As New TextBox()
    Private ReadOnly countLabel As New ToolStripStatusLabel()
    Private miniMode As Boolean = False
    Private normalBounds As Rectangle
    Private normalMinimumSize As Size
    Private Const ClipboardRowPrefix As String = "CMENU_ROW_V1:"
    Private selectedId As Integer
    Private sortFields As New List(Of (Field As String, Descending As Boolean))()
    Private activeFilters As New List(Of (Field As String, OperatorName As String, Value As String))()

    Public Sub New()
        Text = "C-Menu_Publier"
        Size = New Size(1400, 770)
        MinimumSize = New Size(1100, 620)
        StartPosition = FormStartPosition.CenterScreen
        Font = New Font("Segoe UI", 10.0F)
        BackColor = Color.FromArgb(241, 247, 237)
        BuildUI()
        ApplyColors()
        ClearForm()
        ReloadGrid()
        AddHandler titleClock.Tick, Sub(sender, e) UpdateWindowTitle()
        AddHandler FormClosed, Sub(sender, e) titleClock.Stop()
        UpdateWindowTitle()
        titleClock.Start()
    End Sub

    Private Sub UpdateWindowTitle()
        Dim nowValue = DateTime.Now
        Text = "C-Menu_Publier — Nous sommes le " &
            nowValue.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR")) &
            " à " & nowValue.ToString("HH:mm:ss")
    End Sub

    Private Const WM_SYSCOMMAND As Integer = &H112
    Private Const SC_MAXIMIZE As Integer = &HF030
    Private Const SC_RESTORE As Integer = &HF120

    Protected Overrides Sub WndProc(ByRef m As Message)
        If m.Msg = WM_SYSCOMMAND Then
            Dim command = CInt(m.WParam.ToInt64() And &HFFF0L)
            If command = SC_MAXIMIZE OrElse (miniMode AndAlso command = SC_RESTORE) Then
                ToggleMiniMode()
                Return
            End If
        End If
        MyBase.WndProc(m)
    End Sub

    Private Sub ToggleMiniMode()
        MiniMaxClicked(Me, EventArgs.Empty)
    End Sub

    Private Sub MiniMaxClicked(sender As Object, e As EventArgs)
        If Not miniMode Then
            If WindowState = FormWindowState.Maximized Then WindowState = FormWindowState.Normal
            normalBounds = Bounds
            normalMinimumSize = MinimumSize
            MinimumSize = Size.Empty
            Dim targetWidth = Math.Max(180, CInt(Math.Round(normalBounds.Width * 0.1)))
            Dim targetHeight = Math.Max(100, CInt(Math.Round(normalBounds.Height * 0.1)))
            Dim workArea = Screen.FromRectangle(normalBounds).WorkingArea
            Dim pixelsPerCmX As Single
            Dim pixelsPerCmY As Single
            Using graphics = CreateGraphics()
                pixelsPerCmX = graphics.DpiX / 2.54F
                pixelsPerCmY = graphics.DpiY / 2.54F
            End Using
            Dim targetLeft = workArea.Left + CInt(Math.Round(5 * pixelsPerCmX))
            Dim targetTop = workArea.Top + CInt(Math.Round(0.5 * pixelsPerCmY))
            targetLeft = Math.Min(targetLeft, workArea.Right - targetWidth)
            targetTop = Math.Min(targetTop, workArea.Bottom - targetHeight)
            Bounds = New Rectangle(targetLeft, targetTop, targetWidth, targetHeight)
            miniMode = True
        Else
            MinimumSize = New Size(640, 440)
            Bounds = normalBounds
            MinimumSize = normalMinimumSize
            miniMode = False
        End If
    End Sub

    Private Function ButtonFor(title As String, handler As EventHandler) As Button
        Dim b As New Button With {
            .Text = title, .Size = New Size(88, 28),
            .Margin = New Padding(0, 0, 4, 0),
            .BackColor = Color.FromArgb(223, 235, 253),
            .ForeColor = Color.FromArgb(25, 55, 83),
            .FlatStyle = FlatStyle.Flat, .Cursor = Cursors.Hand
        }
        If title = "Sauvegarder" Then b.Width = 112
        If title = "Créer Menu" Then b.Width = 112
        b.FlatAppearance.BorderColor = Color.FromArgb(167, 193, 225)
        AddHandler b.Click, handler
        Return b
    End Function

    Private Sub OpenStarterApplis(sender As Object, e As EventArgs)
        Using dialog As New StarterApplisForm()
            dialog.ShowDialog(Me)
        End Using
    End Sub

    Private Sub BackupDataClicked(sender As Object, e As EventArgs)
        Using dialog As New SaveFileDialog With {
            .Title = "Sauvegarde C-Menu",
            .Filter = "Base SQLite (*.db)|*.db",
            .FileName = "CMenu-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & ".db"
        }
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                Using source As New Microsoft.Data.Sqlite.SqliteConnection(GetActiveDatabaseConnection())
                    source.Open()
                    Using destination As New Microsoft.Data.Sqlite.SqliteConnection("Data Source=" & dialog.FileName)
                        destination.Open()
                        source.BackupDatabase(destination)
                    End Using
                End Using
                MessageBox.Show(Me, "Sauvegarde terminée.")
            Catch ex As Exception
                MessageBox.Show(Me, "Sauvegarde impossible : " & ex.Message)
            End Try
        End Using
    End Sub

    Private Function GetActiveDatabaseConnection() As String
#If DISTRIBUTED Then
        Return "Data Source=" & Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "Menu", "C_Menu.db")
#Else
        Return "Data Source=" & Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "C-Menu", "CMenu.db")
#End If
    End Function

    Private Sub RestoreDataClicked(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog With {.Title = "Restauration C-Menu", .Filter = "Base SQLite (*.db)|*.db"}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            If MessageBox.Show(Me, "Restaurer cette base ? Une sauvegarde de sécurité sera créée.", "Confirmation",
                               MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return
            Try
                Dim backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "C-Menu-Sauvegardes")
                Directory.CreateDirectory(backupDir)
                Dim backupPath = Path.Combine(backupDir, "AvantRestauration-" & DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") & ".db")
                Using target As New Microsoft.Data.Sqlite.SqliteConnection(GetActiveDatabaseConnection())
                    target.Open()
                    Using backup As New Microsoft.Data.Sqlite.SqliteConnection("Data Source=" & backupPath)
                        backup.Open()
                        target.BackupDatabase(backup)
                    End Using
                    Using source As New Microsoft.Data.Sqlite.SqliteConnection("Data Source=" & dialog.FileName & ";Mode=ReadOnly")
                        source.Open()
                        source.BackupDatabase(target)
                    End Using
                End Using
                ClearForm()
                ReloadGrid()
                MessageBox.Show(Me, "Restauration terminée.")
            Catch ex As Exception
                MessageBox.Show(Me, "Restauration impossible : " & ex.Message)
            End Try
        End Using
    End Sub

    Private Sub BuildUI()
        toolbarPanel = New FlowLayoutPanel With {
            .Dock = DockStyle.Top, .Height = 42, .Padding = New Padding(8, 6, 0, 5),
            .WrapContents = False, .AutoScroll = True,
            .BackColor = Color.FromArgb(226, 234, 240)
        }
        toolbarPanel.Controls.Add(ButtonFor("Nouveau", AddressOf NewClicked))
        toolbarPanel.Controls.Add(ButtonFor("Enregistrer", AddressOf SaveClicked))
        toolbarPanel.Controls.Add(ButtonFor("Modifier", AddressOf UpdateClicked))
        toolbarPanel.Controls.Add(ButtonFor("Supprimer", AddressOf DeleteClicked))
        toolbarPanel.Controls.Add(ButtonFor("Dupliquer", AddressOf DuplicateClicked))
        toolbarPanel.Controls.Add(ButtonFor("Filtrer", AddressOf FilterClicked))
        toolbarPanel.Controls.Add(ButtonFor("A-Z", AddressOf SortClicked))
        toolbarPanel.Controls.Add(ButtonFor("Couleurs", AddressOf ColorsClicked))
        toolbarPanel.Controls.Add(ButtonFor("Créer Menu", AddressOf OpenMenuClicked))
#If Not DISTRIBUTED Then
        toolbarPanel.Controls.Add(ButtonFor("Publier", AddressOf PublishCMenuClicked))
        toolbarPanel.Controls.Add(ButtonFor("Sauvegarder", AddressOf BackupClicked))
        toolbarPanel.Controls.Add(ButtonFor("Restaurer", AddressOf RestoreClicked))
#End If

        ' Adapter la largeur de la fenêtre à la somme des boutons, sans barre de défilement.
        Dim toolbarWidth = toolbarPanel.Padding.Left + toolbarPanel.Padding.Right
        For Each control As Control In toolbarPanel.Controls
            toolbarWidth += control.Width + control.Margin.Left + control.Margin.Right
        Next
        Dim requiredFormWidth = toolbarWidth + (Width - ClientSize.Width) + 12
        MinimumSize = New Size(requiredFormWidth, MinimumSize.Height)
        Width = requiredFormWidth

        editorPanel = New TableLayoutPanel With {
            .Dock = DockStyle.Top, .Height = 162, .ColumnCount = 6, .RowCount = 3,
            .BackColor = Color.FromArgb(29, 171, 79),
            .Padding = New Padding(14, 4, 14, 4)
        }
        For i = 1 To 6
            editorPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / 6))
        Next
        editorPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 33.0F))
        editorPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 33.0F))
        editorPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 34.0F))
        AddField(editorPanel, "N° Saisie", idBox, 0, 0, 1, True)
        AddField(editorPanel, "Saisie Le", dateCreated, 1, 0, 1, True)
        AddField(editorPanel, "Modifier Le", dateModified, 2, 0, 1, True)
        AddField(editorPanel, "N° Menu", "NumMenu", 3, 0)
        AddField(editorPanel, "N° Sous-Menu", "NumSousMenu", 4, 0)
        AddField(editorPanel, "Frm_Menu", "Frm_Menu", 5, 0)
        AddField(editorPanel, "Menu", "Menu", 0, 1)
        AddField(editorPanel, "Sous-Menu", "SousMenu", 1, 1)
        AddField(editorPanel, "Info_01", "Info01", 2, 1, 2)
        AddField(editorPanel, "Info_02", "Info02", 4, 1, 2)
        AddField(editorPanel, "Application", "Application", 0, 2, 1)
        AddField(editorPanel, "Action", "Action", 1, 2, 1)
        AddField(editorPanel, "Chemin", "Chemin", 2, 2, 4)

        Dim searchPanel As New TableLayoutPanel With {
            .Dock = DockStyle.Top, .Height = 60, .ColumnCount = 2,
            .Padding = New Padding(16, 10, 16, 8),
            .BackColor = Color.FromArgb(241, 247, 237)
        }
        searchPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 135))
        searchPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        Dim searchLabel As New Label With {
            .Text = "Recherche :", .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font(Font, FontStyle.Bold)
        }
        searchBox.Dock = DockStyle.Fill
        searchBox.PlaceholderText = "Recherche rapide (Entrée pour appliquer)"
        AddHandler searchBox.KeyDown, Sub(sender, e)
                                          If e.KeyCode = Keys.Enter Then
                                              e.SuppressKeyPress = True
                                              ReloadGrid()
                                          End If
                                      End Sub
        searchPanel.Controls.Add(searchLabel, 0, 0)
        searchPanel.Controls.Add(searchBox, 1, 0)

        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.MultiSelect = False
        grid.RowHeadersVisible = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersHeight = 42
        grid.RowTemplate.Height = 34
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 119, 140)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White
        grid.ColumnHeadersDefaultCellStyle.Font = New Font(Font, FontStyle.Bold)
        grid.DefaultCellStyle.BackColor = Color.White
        grid.DefaultCellStyle.ForeColor = Color.FromArgb(35, 48, 57)
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(66, 150, 178)
        grid.DefaultCellStyle.SelectionForeColor = Color.White
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(225, 244, 216)
        grid.GridColor = Color.FromArgb(207, 222, 202)
        For Each spec In New (String, String, Integer)() {
            ("Id", "N° Saisie", 65), ("NumMenu", "N° Menu", 75),
            ("NumSousMenu", "N° Sous-Menu", 90), ("Frm_Menu", "Frm_Menu", 120), ("Menu", "Menu", 130),
            ("SousMenu", "Sous-Menu", 90), ("Info01", "Info_01", 90),
            ("Info02", "Info_02", 90), ("Application", "Application", 90), ("Action", "Action", 90),
            ("Chemin", "Chemin", 270), ("SaisieLe", "Saisie Le", 115),
            ("ModifierLe", "Modifier Le", 115)}
            Dim column As New DataGridViewTextBoxColumn With {
                .DataPropertyName = spec.Item1, .Name = spec.Item1,
                .HeaderText = spec.Item2, .MinimumWidth = spec.Item3,
                .FillWeight = CSng(spec.Item3)
            }
            If spec.Item1 = "SaisieLe" OrElse spec.Item1 = "ModifierLe" Then
                column.DefaultCellStyle.Format = "dd/MM/yyyy HH:mm"
            End If
            grid.Columns.Add(column)
        Next
        Dim rowMenu As New ContextMenuStrip()
        rowMenu.Items.Add("Copier la ligne (Ctrl+C)", Nothing, AddressOf CopyRowClicked)
        rowMenu.Items.Add("Coller comme nouvelle ligne (Ctrl+V)", Nothing, AddressOf PasteRowClicked)
        rowMenu.Items.Add("Dupliquer la ligne (Ctrl+D)", Nothing, AddressOf DuplicateClicked)
        grid.ContextMenuStrip = rowMenu
        AddHandler grid.KeyDown, AddressOf GridKeyDown
        AddHandler grid.CellMouseDown, Sub(sender, e)
            If e.Button = MouseButtons.Right AndAlso e.RowIndex >= 0 Then
                grid.ClearSelection()
                grid.Rows(e.RowIndex).Selected = True
                grid.CurrentCell = grid.Rows(e.RowIndex).Cells(0)
            End If
        End Sub
        AddHandler grid.SelectionChanged, AddressOf GridSelectionChanged

        Dim gridHost As New Panel With {
            .Dock = DockStyle.Fill, .Padding = New Padding(16, 0, 16, 10),
            .BackColor = Color.FromArgb(241, 247, 237)
        }
        gridHost.Controls.Add(grid)
        Dim status As New StatusStrip()
        status.Items.Add(countLabel)
        status.Items.Add(New ToolStripStatusLabel(" | SQLite • Données locales"))
        Controls.Add(gridHost)
        Controls.Add(searchPanel)
        Controls.Add(editorPanel)
        Controls.Add(toolbarPanel)
        Controls.Add(status)
    End Sub

    Private Sub AddField(layout As TableLayoutPanel, title As String, key As String, col As Integer, row As Integer, Optional span As Integer = 1)
        Dim field As Control
        If comboKeys.Contains(key) Then
            field = New ComboBox With {
                .DropDownStyle = ComboBoxStyle.DropDown,
                .AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                .AutoCompleteSource = AutoCompleteSource.ListItems,
                .IntegralHeight = False, .DropDownHeight = 240
            }
        Else
            field = New TextBox()
        End If
        If key = "NumMenu" OrElse key = "NumSousMenu" Then
            Dim numericBox = DirectCast(field, TextBox)
            AddHandler numericBox.KeyPress, Sub(sender, e)
                If Not Char.IsControl(e.KeyChar) AndAlso Not Char.IsDigit(e.KeyChar) Then
                    e.Handled = True
                End If
            End Sub
        End If
        fields.Add(key, field)
        AddField(layout, title, field, col, row, span, False)
    End Sub

    Private Sub AddField(layout As TableLayoutPanel, title As String, tb As Control, col As Integer, row As Integer, span As Integer, readOnlyField As Boolean)
        Dim panel As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2,
            .Margin = New Padding(5, 0, 5, 0)
        }
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 19))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        Dim fieldLabel As New Label With {
            .Text = title, .Dock = DockStyle.Fill,
            .ForeColor = Color.Black, .TextAlign = ContentAlignment.MiddleLeft
        }
        fieldLabels.Add(fieldLabel)
        panel.Controls.Add(fieldLabel, 0, 0)
        tb.Dock = DockStyle.Top
        If TypeOf tb Is TextBox Then DirectCast(tb, TextBox).ReadOnly = readOnlyField
        tb.BackColor = If(readOnlyField, Color.FromArgb(232, 237, 232), Color.White)
        panel.Controls.Add(tb, 0, 1)
        layout.Controls.Add(panel, col, row)
        If span > 1 Then layout.SetColumnSpan(panel, span)
    End Sub

    Private Sub ApplyColors()
        toolbarPanel.BackColor = Color.FromArgb(themeColors.Toolbar)
        editorPanel.BackColor = Color.FromArgb(themeColors.Entry)
        For Each fieldLabel In fieldLabels
            fieldLabel.ForeColor = Color.FromArgb(themeColors.Labels)
        Next
        grid.BackgroundColor = Color.FromArgb(themeColors.Table)
        grid.DefaultCellStyle.BackColor = Color.FromArgb(themeColors.Table)
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(themeColors.Table)
        grid.Invalidate()
    End Sub

    Private Sub ColorsClicked(sender As Object, e As EventArgs)
        Using dialog As New Frm_Couleurs(themeColors)
            If dialog.ShowDialog(Me) = DialogResult.OK Then
                themeColors = dialog.SelectedColors.Copy()
                ApplyColors()
                Try
                    themeColors.Save()
                Catch ex As Exception
                    MessageBox.Show("Les couleurs sont appliquées mais n'ont pas pu être enregistrées : " & ex.Message, "Couleurs")
                End Try
            End If
        End Using
    End Sub

    Private Sub ReloadGrid()
        RemoveHandler grid.SelectionChanged, AddressOf GridSelectionChanged
        Dim data = repo.GetAll(searchBox.Text.Trim(), sortFields, activeFilters)
        grid.DataSource = New BindingList(Of MenuItem)(data)
        RefreshCombos(data)
        countLabel.Text = data.Count.ToString() & " enregistrement(s)"
        grid.ClearSelection()
        AddHandler grid.SelectionChanged, AddressOf GridSelectionChanged
    End Sub

    Private Sub RefreshCombos(data As List(Of MenuItem))
        For Each key In comboKeys
            Dim combo = DirectCast(fields(key), ComboBox)
            Dim previous = combo.Text
            Dim values As New SortedSet(Of String)(StringComparer.CurrentCultureIgnoreCase)
            For Each item In data
                Dim value As String = ""
                Select Case key
                    Case "Frm_Menu" : value = item.Frm_Menu
                    Case "Menu" : value = item.Menu
                    Case "SousMenu" : value = item.SousMenu
                    Case "Info01" : value = item.Info01
                    Case "Info02" : value = item.Info02
                    Case "Application" : value = item.Application
                    Case "Action" : value = item.Action
                End Select
                If Not String.IsNullOrWhiteSpace(value) Then values.Add(value.Trim())
            Next
            combo.BeginUpdate()
            combo.Items.Clear()
            combo.Items.AddRange(values.Cast(Of Object)().ToArray())
            combo.EndUpdate()
            combo.Text = previous
        Next
    End Sub

    Private Sub ClearForm()
        selectedId = 0
        idBox.Clear()
        dateCreated.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        dateModified.Clear()
        For Each tb In fields.Values
            tb.Text = ""
        Next
    End Sub

    Private Function CurrentItem() As MenuItem
        Return New MenuItem With {
            .Id = selectedId, .NumMenu = fields("NumMenu").Text.Trim(),
            .NumSousMenu = fields("NumSousMenu").Text.Trim(),
            .Frm_Menu = fields("Frm_Menu").Text.Trim(),
            .Menu = fields("Menu").Text.Trim(),
            .SousMenu = fields("SousMenu").Text.Trim(),
            .Info01 = fields("Info01").Text.Trim(),
            .Info02 = fields("Info02").Text.Trim(),
            .Application = fields("Application").Text.Trim(),
            .Action = fields("Action").Text.Trim(),
            .Chemin = fields("Chemin").Text.Trim()
        }
    End Function

    Private Function IsValid(item As MenuItem) As Boolean
        If item.NumMenu = "" OrElse item.Menu = "" Then
            MessageBox.Show("Les champs N° Menu et Menu sont obligatoires.", "Validation")
            Return False
        End If
        Dim number As Long
        If Not Long.TryParse(item.NumMenu, number) OrElse number < 0 Then
            MessageBox.Show("N° Menu doit être un nombre entier positif ou zéro.", "Validation")
            fields("NumMenu").Focus()
            Return False
        End If
        If item.NumSousMenu <> "" AndAlso (Not Long.TryParse(item.NumSousMenu, number) OrElse number < 0) Then
            MessageBox.Show("N° Sous-Menu doit être un nombre entier positif ou zéro.", "Validation")
            fields("NumSousMenu").Focus()
            Return False
        End If
        Return True
    End Function

    Private Sub NewClicked(sender As Object, e As EventArgs)
        grid.ClearSelection()
        ClearForm()
        fields("NumMenu").Focus()
    End Sub

    Private Function IsProtectedExample(id As Integer) As Boolean
        If id = 0 Then Return False
        Return repo.GetAll().Any(Function(entry) entry.Id = id AndAlso
            String.Equals(entry.Menu.Trim(), "Exemple", StringComparison.OrdinalIgnoreCase) AndAlso
            String.Equals(entry.NumMenu.Trim(), "99", StringComparison.OrdinalIgnoreCase))
    End Function

    Private Sub ShowProtectedExampleMessage()
        MessageBox.Show(Me, "Ceci sont des exemples ni modifiables ni supprimables.", "Enregistrements protégés",
                        MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub SaveClicked(sender As Object, e As EventArgs)
        Dim item = CurrentItem()
        If Not IsValid(item) Then Return
        If item.Id = 0 Then
            ' Retrouver une ligne existante par son identité de menu, sans tenir compte du filtre affiché.
            Dim existing = repo.GetAll().FirstOrDefault(Function(entry) _
                String.Equals(entry.Frm_Menu, item.Frm_Menu, StringComparison.OrdinalIgnoreCase) AndAlso
                String.Equals(entry.NumMenu, item.NumMenu, StringComparison.OrdinalIgnoreCase) AndAlso
                String.Equals(entry.NumSousMenu, item.NumSousMenu, StringComparison.OrdinalIgnoreCase))
            If existing IsNot Nothing Then item.Id = existing.Id
        End If
        If item.Id <> 0 Then
            If IsProtectedExample(item.Id) Then
                ShowProtectedExampleMessage()
                Return
            End If
            repo.Update(item)
        Else
            repo.Insert(item)
        End If
        ClearForm()
        ReloadGrid()
    End Sub

    Private Sub UpdateClicked(sender As Object, e As EventArgs)
        If selectedId = 0 Then
            MessageBox.Show("Sélectionnez d'abord un enregistrement.")
            Return
        End If
        If IsProtectedExample(selectedId) Then
            ShowProtectedExampleMessage()
            Return
        End If
        Dim item = CurrentItem()
        If Not IsValid(item) Then Return
        repo.Update(item)
        ClearForm()
        ReloadGrid()
    End Sub

    Private Function SelectedRowCopy() As MenuItem
        If grid.SelectedRows.Count = 0 Then Return Nothing
        Dim source = TryCast(grid.SelectedRows(0).DataBoundItem, MenuItem)
        If source Is Nothing Then Return Nothing
        Return New MenuItem With {
            .NumMenu = source.NumMenu, .NumSousMenu = source.NumSousMenu,
            .Frm_Menu = source.Frm_Menu, .Menu = source.Menu,
            .SousMenu = source.SousMenu, .Info01 = source.Info01,
            .Info02 = source.Info02, .Application = source.Application,
            .Action = source.Action, .Chemin = source.Chemin
        }
    End Function

    Private Sub CopyRowClicked(sender As Object, e As EventArgs)
        Dim item = SelectedRowCopy()
        If item Is Nothing Then
            MessageBox.Show("Sélectionnez une ligne à copier.", "Copier")
            Return
        End If
        Try
            Clipboard.SetText(ClipboardRowPrefix & JsonSerializer.Serialize(item))
        Catch ex As Exception
            MessageBox.Show("Impossible de copier la ligne : " & ex.Message, "Copier",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub PasteRowClicked(sender As Object, e As EventArgs)
        Try
            If Not Clipboard.ContainsText() Then
                MessageBox.Show("Copiez d'abord une ligne dans MenuPublier ou C-Menu.", "Coller")
                Return
            End If
            Dim clipboardText = Clipboard.GetText()
            If Not clipboardText.StartsWith(ClipboardRowPrefix, StringComparison.Ordinal) Then
                MessageBox.Show("Le presse-papiers ne contient pas une ligne C-Menu.", "Coller")
                Return
            End If
            Dim item = JsonSerializer.Deserialize(Of MenuItem)(clipboardText.Substring(ClipboardRowPrefix.Length))
            If item Is Nothing Then Throw New InvalidDataException("Ligne copiée invalide.")
            item.Id = 0
            If Not IsValid(item) Then Return
            repo.Insert(item)
            ClearForm()
            ReloadGrid()
        Catch ex As Exception
            MessageBox.Show("Impossible de coller la ligne : " & ex.Message, "Coller",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub DuplicateClicked(sender As Object, e As EventArgs)
        Dim item = SelectedRowCopy()
        If item Is Nothing Then
            MessageBox.Show("Sélectionnez une ligne à dupliquer.", "Dupliquer")
            Return
        End If
        If Not IsValid(item) Then Return
        repo.Insert(item)
        ClearForm()
        ReloadGrid()
    End Sub

    Private Sub GridKeyDown(sender As Object, e As KeyEventArgs)
        If Not e.Control Then Return
        Select Case e.KeyCode
            Case Keys.C
                CopyRowClicked(sender, EventArgs.Empty)
            Case Keys.V
                PasteRowClicked(sender, EventArgs.Empty)
            Case Keys.D
                DuplicateClicked(sender, EventArgs.Empty)
            Case Else
                Return
        End Select
        e.Handled = True
        e.SuppressKeyPress = True
    End Sub

    Private Sub DeleteClicked(sender As Object, e As EventArgs)
        If selectedId = 0 Then
            MessageBox.Show("Sélectionnez d'abord un enregistrement.")
            Return
        End If
        If IsProtectedExample(selectedId) Then
            ShowProtectedExampleMessage()
            Return
        End If
        If MessageBox.Show("Supprimer cet enregistrement ?", "Confirmation",
                           MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
            repo.Delete(selectedId)
            ClearForm()
            ReloadGrid()
        End If
    End Sub

    Private Sub FilterClicked(sender As Object, e As EventArgs)
        Using dialog As New Frm_Filtre(repo.GetAll(), activeFilters)
            If dialog.ShowDialog(Me) = DialogResult.OK Then
                activeFilters = dialog.Filters
                ClearForm()
                ReloadGrid()
            End If
        End Using
    End Sub

#If Not DISTRIBUTED Then
    Private Async Sub BackupClicked(sender As Object, e As EventArgs)
        Using dialog As New SaveFileDialog With {
            .Title = "Sauvegarder et publier la base en ZIP",
            .Filter = "Archive ZIP (*.zip)|*.zip",
            .DefaultExt = "zip",
            .FileName = "MenuPublier_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & ".zip"
        }
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Dim tempDb = Path.Combine(Path.GetTempPath(), "CMenuBackup-" & Guid.NewGuid().ToString("N") & ".db")
            Try
                Cursor = Cursors.WaitCursor
                Dim exePath = Await CMenuPublisher.PublishAsync()
                Dim sourceRoot = Path.Combine(AppContext.BaseDirectory, "Sources")
                If Not File.Exists(Path.Combine(sourceRoot, "C-Menu.vbproj")) Then
                    Dim current = New DirectoryInfo(AppContext.BaseDirectory)
                    While current IsNot Nothing
                        If File.Exists(Path.Combine(current.FullName, "C-Menu.vbproj")) Then
                            sourceRoot = current.FullName
                            Exit While
                        End If
                        current = current.Parent
                    End While
                End If
                If Not File.Exists(Path.Combine(sourceRoot, "C-Menu.vbproj")) Then
                    Throw New FileNotFoundException("Sources introuvables : le dossier Sources doit accompagner C-Menu_Publier.exe.")
                End If
                PublisherBackup.SaveTo(tempDb)
                ' SQLite peut garder un handle ouvert brièvement après BackupDatabase.
                ' Ouvrir le fichier en lecture partagée évite le verrouillage lors du ZIP.
                Using archive = ZipFile.Open(dialog.FileName, ZipArchiveMode.Create)
                    archive.CreateEntryFromFile(exePath, "C-Menu.exe", CompressionLevel.Optimal)
                    For Each sourceFile In Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                        Dim relative = Path.GetRelativePath(sourceRoot, sourceFile)
                        Dim segments = relative.Split(Path.DirectorySeparatorChar)
                        If segments.Any(Function(part) {".git", ".vs", "bin", "obj"}.Contains(part, StringComparer.OrdinalIgnoreCase)) Then Continue For
                        If {".db", ".exe", ".zip", ".pdb"}.Contains(Path.GetExtension(sourceFile), StringComparer.OrdinalIgnoreCase) Then Continue For
                        archive.CreateEntryFromFile(sourceFile, "Sources/" & relative.Replace(Path.DirectorySeparatorChar, "/"c), CompressionLevel.Optimal)
                    Next
                    Dim entry = archive.CreateEntry("CMenu.db", CompressionLevel.Optimal)
                    Using input = New FileStream(tempDb, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
                        Using output = entry.Open()
                            input.CopyTo(output)
                        End Using
                    End Using
                End Using
                Dim publishDir = Path.Combine("C:\Menu", "Sauvegardes")
                Directory.CreateDirectory(publishDir)
                Dim publishedPath = Path.Combine(publishDir, Path.GetFileName(dialog.FileName))
                If Not Path.GetFullPath(publishedPath).Equals(Path.GetFullPath(dialog.FileName), StringComparison.OrdinalIgnoreCase) Then
                    File.Copy(dialog.FileName, publishedPath, True)
                End If
                MessageBox.Show("Programme C-Menu.exe, base CMenu.db et sources sauvegardés en ZIP :" & Environment.NewLine & dialog.FileName &
                                Environment.NewLine & Environment.NewLine & "Copie publiée :" & Environment.NewLine & publishedPath,
                                "Sauvegarder")
                Process.Start(New ProcessStartInfo("explorer.exe", "/select,""" & dialog.FileName & """") With {.UseShellExecute = True})
            Catch ex As Exception
                MessageBox.Show("Sauvegarde ou publication impossible : " & ex.Message, "Sauvegarder",
                                MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Cursor = Cursors.Default
                Try
                    If File.Exists(tempDb) Then File.Delete(tempDb)
                Catch
                End Try
            End Try
        End Using
    End Sub

    Private Sub RestoreClicked(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog With {
            .Title = "Restaurer la base de MenuPublier",
            .Filter = "Sauvegarde ZIP ou SQLite (*.zip;*.db)|*.zip;*.db|Archive ZIP (*.zip)|*.zip|Base SQLite (*.db)|*.db",
            .CheckFileExists = True
        }
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            If MessageBox.Show("Remplacer toutes les données actuelles par cette sauvegarde ?" &
                               Environment.NewLine & dialog.FileName, "Confirmer la restauration",
                               MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return
            Dim tempDb As String = Nothing
            Try
                Dim sourcePath = dialog.FileName
                If Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase) Then
                    tempDb = Path.Combine(Path.GetTempPath(), "CMenuRestore-" & Guid.NewGuid().ToString("N") & ".db")
                    Using archive = ZipFile.OpenRead(sourcePath)
                        Dim entry = archive.GetEntry("CMenu.db")
                        If entry Is Nothing Then Throw New InvalidDataException("Le ZIP ne contient pas CMenu.db.")
                        entry.ExtractToFile(tempDb)
                    End Using
                    sourcePath = tempDb
                End If
                PublisherBackup.RestoreFrom(sourcePath)
                activeFilters.Clear()
                sortFields.Clear()
                searchBox.Clear()
                ClearForm()
                ReloadGrid()
                MessageBox.Show("Base restaurée avec succès.", "Restaurer")
            Catch ex As Exception
                MessageBox.Show("Restauration impossible : " & ex.Message, "Restaurer",
                                MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Try
                    If tempDb IsNot Nothing AndAlso File.Exists(tempDb) Then File.Delete(tempDb)
                Catch
                End Try
            End Try
        End Using
    End Sub

    Private Async Sub PublishCMenuClicked(sender As Object, e As EventArgs)
        Try
            Cursor = Cursors.WaitCursor
            Dim exePath = Await CMenuPublisher.PublishAsync()
            Cursor = Cursors.Default
            MessageBox.Show("C-Menu indépendant créé :" & Environment.NewLine & exePath, "Publier")
            Process.Start(New ProcessStartInfo("explorer.exe", "/select,""" & exePath & """") With {.UseShellExecute = True})
        Catch ex As Exception
            Cursor = Cursors.Default
            MessageBox.Show("Publication impossible :" & Environment.NewLine & ex.Message, "Publier",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
#End If

    Private Async Sub OpenMenuClicked(sender As Object, e As EventArgs)
        If selectedId = 0 Then
            MessageBox.Show("Sélectionnez un enregistrement dans le tableau.", "Ouvrir menu")
            Return
        End If
        Dim selected = TryCast(grid.SelectedRows.Cast(Of DataGridViewRow)().
            Select(Function(row) row.DataBoundItem).FirstOrDefault(), MenuItem)
        If selected Is Nothing OrElse String.IsNullOrWhiteSpace(selected.Frm_Menu) Then
            MessageBox.Show("L'enregistrement doit contenir une valeur Frm_Menu.", "Ouvrir menu")
            Return
        End If
        Try
            Cursor = Cursors.WaitCursor
            Dim exePath = Await MenuExeGenerator.CreateAsync(selected.Frm_Menu)
            Cursor = Cursors.Default
            Dim explorerStart As New ProcessStartInfo("explorer.exe") With {.UseShellExecute = True}
            explorerStart.ArgumentList.Add(Path.GetDirectoryName(exePath))
            Process.Start(explorerStart)
        Catch ex As Exception
            Cursor = Cursors.Default
            MessageBox.Show("Échec de création de l'exécutable :" & Environment.NewLine & ex.Message,
                            "Créer Menu", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub SortClicked(sender As Object, e As EventArgs)
        Using dialog As New Frm_Tri(sortFields)
            If dialog.ShowDialog(Me) = DialogResult.OK Then
                sortFields = dialog.SortFields
                ClearForm()
                ReloadGrid()
            End If
        End Using
    End Sub

    Private Sub GridSelectionChanged(sender As Object, e As EventArgs)
        If grid.SelectedRows.Count = 0 Then Return
        Dim item = TryCast(grid.SelectedRows(0).DataBoundItem, MenuItem)
        If item Is Nothing Then Return
        selectedId = item.Id
        idBox.Text = item.Id.ToString()
        dateCreated.Text = item.SaisieLe.ToString("dd/MM/yyyy HH:mm")
        dateModified.Text = If(item.ModifierLe.HasValue,
                               item.ModifierLe.GetValueOrDefault().ToString("dd/MM/yyyy HH:mm"), "")
        fields("NumMenu").Text = item.NumMenu
        fields("NumSousMenu").Text = item.NumSousMenu
        fields("Frm_Menu").Text = item.Frm_Menu
        fields("Menu").Text = item.Menu
        fields("SousMenu").Text = item.SousMenu
        fields("Info01").Text = item.Info01
        fields("Info02").Text = item.Info02
        fields("Application").Text = item.Application
        fields("Action").Text = item.Action
        fields("Chemin").Text = item.Chemin
    End Sub
End Class
